using Ardalis.Result;
using CpuUsage.Core.Models;
using CpuUsage.Core.Platform.General;

namespace CpuUsage.Core.Platform.Linux;

internal class LinuxProcessUsageProvider : IProcessUsageProvider
{
    private readonly int _cpuCoreCount;
    private readonly MemorySize _totalMemoryBytes;
    private readonly int _currentProcessId;

    private long _previousTotalCpuTime;

    // Предыдущие срезы по процессам (pid -> ProcStat)
    private readonly Dictionary<int, ProcStat> _previousProcessStats = new ();

    // Предыдущие срезы по потокам (pid -> tid -> ProcStat)
    private readonly Dictionary<int, Dictionary<int, ProcStat>> _previousThreadStats = new ();

    public LinuxProcessUsageProvider()
    {
        _cpuCoreCount = Environment.ProcessorCount;
        _currentProcessId = Environment.ProcessId;

        Result<MemorySize> totalMemorySizeResult = GetTotalMemoryBytes();
        _totalMemoryBytes = totalMemorySizeResult.IsSuccess
            ? totalMemorySizeResult.Value
            : throw new Exception(totalMemorySizeResult.Errors.ToErrorList().ToString());
    }

    public void Dispose()
    {
        _previousProcessStats.Clear();
        _previousThreadStats.Clear();
    }

    public Result<ProcessUsage> GetCurrentProcessUsage(bool includeChildren = true, bool includeThreads = true)
        => GetProcessUsage(_currentProcessId, includeChildren, includeThreads);

    private Result<ProcessUsage> GetProcessUsage(int pid, bool includeChildren = true, bool includeThreads = true)
    {
        // Читаем текущий срез системного CPU и процесса
        Result<long> totalCpuRes = LinuxProcessUsageProvider.ReadTotalCpuTime();
        if (!totalCpuRes.IsSuccess)
        {
            return Result<ProcessUsage>.Error(totalCpuRes.Errors.ToErrorList());
        }

        Result<ProcStat> procStatRes = LinuxProcessUsageProvider.ReadProcStat(pid);
        if (!procStatRes.IsSuccess)
        {
            return Result<ProcessUsage>.Error(procStatRes.Errors.ToErrorList());
        }

        // Если нет предыдущих снимков — сохраняем и возвращаем CPU=0
        if (!_previousProcessStats.TryGetValue(pid, out ProcStat? prevProcStat))
        {
            _previousTotalCpuTime = totalCpuRes.Value;
            _previousProcessStats[pid] = procStatRes.Value;
            _previousThreadStats[pid] = new Dictionary<int, ProcStat>();

            Result<MemorySize> memResFirst = LinuxProcessUsageProvider.ReadProcessMemory(pid);
            MemorySize mb = memResFirst.IsSuccess ? memResFirst.Value : 0;

            return Result<ProcessUsage>.Success(new ProcessUsage
            {
                ProcessId = pid,
                CpuUsagePercentNormalized = 0,
                MemoryBytes = mb,
                MemoryPercent = Calculator.CalculateMemoryPercent(_totalMemoryBytes, mb)
            });
        }

        // Вычисляем дельту системного CPU
        long deltaTotalCpu = totalCpuRes.Value - _previousTotalCpuTime;
        long deltaProcCpu = Calculator.CalculateProcStatDelta(prevProcStat, procStatRes);

        // Сохраняем текущие срезы
        _previousTotalCpuTime = totalCpuRes.Value;
        _previousProcessStats[pid] = procStatRes.Value;

        // Получаем память
        Result<MemorySize> memRes = LinuxProcessUsageProvider.ReadProcessMemory(pid);
        MemorySize memBytes = memRes.IsSuccess ? memRes.Value : 0;

        double usageNormalized = Calculator.CalculateCpuPercentFromDeltasNormalized(deltaTotalCpu, deltaProcCpu);

        ProcessUsage usage = new ()
        {
            ProcessId = pid,
            CpuUsagePercentNormalized = usageNormalized,
            CpuUsagePercent = Calculator.CalculateCpuPercentFromDeltas(usageNormalized, _cpuCoreCount),
            MemoryBytes = memBytes,
            MemoryPercent = Calculator.CalculateMemoryPercent(_totalMemoryBytes, memBytes),
            Children = new List<ProcessUsage>(),
            Threads = new List<ThreadUsage>()
        };

        if (includeThreads)
        {
            Result<List<ThreadUsage>> threadUsagesRes = GetThreadUsages(pid, deltaTotalCpu);
            if (threadUsagesRes.IsSuccess)
            {
                usage.Threads = threadUsagesRes.Value;
            }
            else
            {
                return Result<ProcessUsage>.Error(threadUsagesRes.Errors.ToErrorList());
            }
        }

        return Result<ProcessUsage>.Success(usage);

    }

    private Result<MemorySize> GetTotalMemoryBytes()
        => LinuxProcessUsageProvider.ReadMemorySize("/proc/meminfo", "MemTotal:");

    private static Result<ProcStat> ReadProcStat(int pid)
    {
        try
        {
            string raw = File.ReadAllText($"/proc/{pid}/stat");
            string[] parts = LinuxProcessUsageProvider.ParseStatLine(raw);
            return Result<ProcStat>.Success(new ProcStat
            {
                Pid = int.Parse(parts[0]),
                Comm = parts[1],
                State = parts[2],
                PPid = int.Parse(parts[3]),
                UserTime = long.Parse(parts[13]),
                KernelTime = long.Parse(parts[14])
            });
        }
        catch (Exception ex)
        {
            return Result<ProcStat>.Error($"Ошибка чтения /proc/{pid}/stat: {ex.Message}");
        }
    }

    private static Result<long> ReadTotalCpuTime()
    {
        try
        {
            string? line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
            if (line == null)
            {
                return Result<long>.Error("Не удалось прочитать строку с CPU из /proc/stat");
            }

            IEnumerable<string> parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1);
            long total = 0;
            foreach (string p in parts)
            {
                if (long.TryParse(p, out long val))
                {
                    total += val;
                }
            }

            return Result<long>.Success(total);
        }
        catch (Exception ex)
        {
            return Result<long>.Error($"Ошибка чтения /proc/stat: {ex.Message}");
        }
    }

    private static Result<MemorySize> ReadProcessMemory(int pid) => LinuxProcessUsageProvider.ReadMemorySize($"/proc/{pid}/status", "VmRSS:");

    private Result<List<ThreadUsage>> GetThreadUsages(int pid, long deltaTotalCpu)
    {
        List<ThreadUsage> threadUsages = [];
        Dictionary<int, ProcStat>? prevThreads = _previousThreadStats.GetValueOrDefault(pid);
        Dictionary<int, ProcStat> currentThreads = new ();

        string taskDir = $"/proc/{pid}/task";
        if (!Directory.Exists(taskDir))
        {
            return Result<List<ThreadUsage>>.Success(threadUsages);
        }

        try
        {
            IEnumerable<int> tids = Directory.GetDirectories(taskDir)
                .Select(Path.GetFileName)
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse!);

            foreach (int tid in tids)
            {
                Result<ProcStat> threadStatRes = LinuxProcessUsageProvider.ReadThreadStat(pid, tid);
                if (!threadStatRes.IsSuccess)
                {
                    return Result.Error(threadStatRes.Errors.ToErrorList());
                }

                ProcStat curStat = threadStatRes.Value;

                // Дельта времени для данного потока
                long deltaThreadTime = 0;
                if (prevThreads is not null && prevThreads.TryGetValue(tid, out ProcStat? prevStat))
                {
                    deltaThreadTime = Calculator.CalculateProcStatDelta(prevStat, curStat);
                }


                double cpuNormalized = Calculator.CalculateCpuPercentFromDeltasNormalized(deltaTotalCpu, deltaThreadTime);
                threadUsages.Add(new ThreadUsage
                {
                    ThreadId = tid,
                    CpuUsagePercentNormalized = cpuNormalized,
                    CpuUsagePercent = Calculator.CalculateCpuPercentFromDeltas(cpuNormalized, _cpuCoreCount)
                });

                currentThreads[tid] = curStat;
            }

            // Обновляем кеш потоков процесса
            _previousThreadStats[pid] = currentThreads;

            return Result<List<ThreadUsage>>.Success(threadUsages);
        }
        catch (Exception ex)
        {
            return Result<List<ThreadUsage>>.Error($"Ошибка получения потоков: {ex.Message}");
        }
    }

    private static Result<ProcStat> ReadThreadStat(int pid, int tid)
    {
        try
        {
            string path = $"/proc/{pid}/task/{tid}/stat";
            if (!File.Exists(path))
            {
                return Result<ProcStat>.Error($"Файл {path} не найден.");
            }

            string raw = File.ReadAllText(path);
            string[] parts = LinuxProcessUsageProvider.ParseStatLine(raw);

            return Result<ProcStat>.Success(new ProcStat
            {
                Pid = int.Parse(parts[0]),
                Comm = parts[1],
                State = parts[2],
                PPid = int.Parse(parts[3]),
                UserTime = long.Parse(parts[13]),
                KernelTime = long.Parse(parts[14])
            });
        }
        catch (Exception ex)
        {
            return Result<ProcStat>.Error($"Ошибка чтения {pid}/task/{tid}/stat: {ex.Message}");
        }
    }

    private static Result<MemorySize> ReadMemorySize(string filePath, string memoryName)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Result<MemorySize>.Error($"{filePath} не найден.");
            }

            foreach (string line in File.ReadLines(filePath))
            {
                if (line.StartsWith(memoryName))
                {
                    string[] parts = line.Split((char[]) null!, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                    {
                        return Result<MemorySize>.Success(MemorySize.FromKilobytes(kb));
                    }
                }
            }

            return Result<MemorySize>.Error($"Не удалось прочитать найти строку {memoryName} в {filePath}");
        }
        catch (Exception ex)
        {
            return Result<MemorySize>.Error($"Ошибка чтения памяти {filePath} {memoryName}: {ex.Message}");
        }
    }

    private static string[] ParseStatLine(string statLine)
    {
        int start = statLine.IndexOf('(');
        int end = statLine.LastIndexOf(')');
        string pre = statLine.Substring(0, start - 1).Trim();
        string comm = statLine.Substring(start + 1, end - start - 1);
        string post = statLine.Substring(end + 2).Trim();

        string[] preParts = pre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] postParts = post.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        List<string> allParts = new ();
        allParts.AddRange(preParts);
        allParts.Add(comm);
        allParts.AddRange(postParts);

        return allParts.ToArray();
    }
}
