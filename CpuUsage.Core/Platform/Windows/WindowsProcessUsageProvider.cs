using System.Diagnostics;
using System.Runtime.InteropServices;
using Ardalis.Result;
using CpuUsage.Core.Models;
using CpuUsage.Core.Platform.General;

namespace CpuUsage.Core.Platform.Windows;

internal class WindowsProcessUsageProvider : IProcessUsageProvider
{
    private readonly MemorySize _totalMemoryBytes;
    private readonly int _currentProcessId;
    private readonly int _cpuCoresCount;

    private long _previousTotalCpuTime;

    // Предыдущие срезы по процессам (pid -> ProcStat)
    private readonly Dictionary<int, ProcStat> _previousProcessStats = new ();

    // Предыдущие срезы по потокам (pid -> tid -> ProcStat)
    private readonly Dictionary<int, Dictionary<int, ProcStat>?> _previousThreadStats = new ();


    public WindowsProcessUsageProvider()
    {
        _previousTotalCpuTime = 0;
        _currentProcessId = Environment.ProcessId;
        _cpuCoresCount = Environment.ProcessorCount;

        Result<MemorySize> totalMemorySizeResult = WindowsProcessUsageProvider.GetTotalMemoryBytes();
        _totalMemoryBytes = totalMemorySizeResult.IsSuccess
            ? totalMemorySizeResult.Value
            : throw new Exception(totalMemorySizeResult.Errors.ToErrorList().ToString());

    }

    private static Result<MemorySize> GetTotalMemoryBytes()
    {
        Result<MemoryStatusExternal> result = MemoryStatusExternal.Create();
        if (!result.IsSuccess)
        {
            return Result<MemorySize>.Error(result.Errors.ToErrorList());
        }

        return MemorySize.FromBytes(result.Value.ullTotalPhys);
    }

    public Result<ProcessUsage> GetCurrentProcessUsage(bool includeChildren = true, bool includeThreads = true)
        => GetProcessUsage(_currentProcessId, includeChildren, includeThreads);

    private Result<ProcessUsage> GetProcessUsage(int pid, bool includeChildren = true, bool includeThreads = true)
    {
        Result<long> totalCpuRes = WindowsProcessUsageProvider.GetTotalCpuTime();
        if (!totalCpuRes.IsSuccess)
        {
            return Result<ProcessUsage>.Error(totalCpuRes.Errors.ToErrorList());
        }

        using Process process = Process.GetProcessById(pid);

        Result<ProcStat> currProcStat = WindowsProcessUsageProvider.GetProcStat();
        if (!currProcStat.IsSuccess)
        {
            return Result.Error(currProcStat.Errors.ToErrorList());
        }

        if (!_previousProcessStats.TryGetValue(pid, out ProcStat? prevProcStat))
        {
            _previousTotalCpuTime = totalCpuRes.Value;
            _previousProcessStats[pid] = currProcStat.Value;
            _previousThreadStats[pid] = new Dictionary<int, ProcStat>();

            Result<MemorySize> memResFirst = WindowsProcessUsageProvider.GetProcessMemory();
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
        long deltaProcCpu = Calculator.CalculateProcStatDelta(prevProcStat, currProcStat);

        // Сохраняем текущие срезы
        _previousTotalCpuTime = totalCpuRes.Value;
        _previousProcessStats[pid] = currProcStat.Value;

        // Получаем память
        Result<MemorySize> memRes = WindowsProcessUsageProvider.GetProcessMemory();
        if (!memRes.IsSuccess)
        {
            return Result.Error(memRes.Errors.ToErrorList());
        }
        MemorySize memBytes = memRes.Value;

        double usageNormalized = Calculator.CalculateCpuPercentFromDeltasNormalized(deltaTotalCpu, deltaProcCpu);

        ProcessUsage usage = new ()
        {
            ProcessId = pid,
            CpuUsagePercentNormalized = usageNormalized,
            CpuUsagePercent = Calculator.CalculateCpuPercentFromDeltas(usageNormalized, _cpuCoresCount),
            MemoryBytes = memBytes,
            MemoryPercent = Calculator.CalculateMemoryPercent(_totalMemoryBytes, memBytes),
            Children = new List<ProcessUsage>(),
            Threads = new List<ThreadUsage>()
        };

        if (includeThreads)
        {
            Result<List<ThreadUsage>> threadUsagesRes = GetThreadUsages(process.Id, deltaTotalCpu);
            if (!threadUsagesRes.IsSuccess)
            {
                return Result<ProcessUsage>.Error(threadUsagesRes.Errors.ToErrorList());
            }

            usage.Threads = threadUsagesRes.Value;
        }

        return Result<ProcessUsage>.Success(usage);
    }

    private Result<List<ThreadUsage>> GetThreadUsages(Process process, long deltaTotalCpu)
    {
        try
        {
            ProcessThreadCollection threads = process.Threads;
            List<ThreadUsage> list = new ();
            if (!_previousThreadStats.TryGetValue(process.Id, out Dictionary<int, ProcStat>? prevThreadStats))
            {
                prevThreadStats = new Dictionary<int, ProcStat>();
            }
            Dictionary<int, ProcStat> newStats = new (threads.Count);

            foreach (ProcessThread thread in threads)
            {
                int tid = thread.Id;

                ProcStat currStat = new ()
                {
                    UserTime = thread.UserProcessorTime.Ticks,
                    KernelTime = thread.PrivilegedProcessorTime.Ticks
                };

                long delta = 0;
                if (prevThreadStats!.TryGetValue(tid, out ProcStat? prevStat))
                {
                    delta = Calculator.CalculateProcStatDelta(prevStat, currStat);
                }
                double usage = deltaTotalCpu > 0
                    ? Calculator.CalculateCpuPercentFromDeltasNormalized(deltaTotalCpu, delta)
                    : 0;

                list.Add(new ThreadUsage
                {
                    ThreadId = tid,
                    CpuUsagePercentNormalized = usage,
                    CpuUsagePercent = Calculator.CalculateCpuPercentFromDeltas(usage, _cpuCoresCount)
                });

                newStats[tid] = currStat;
            }
            _previousThreadStats[process.Id] = newStats;
            return Result.Success(list);

        }
        catch (Exception ex)
        {
            return Result<List<ThreadUsage>>.Error(ex.Message);
        }
    }

    private Result<List<ThreadUsage>> GetThreadUsages(int processId, long deltaTotalCpu)
    {
        const uint THREAD_QUERY_INFORMATION = 0x0040;
        List<ThreadUsage> threads = ThreadSnapshot.GetThreadIdsOfProcess(processId)
            .Select(c => new ThreadUsage() { ThreadId = (int) c })
            .ToList();
        
        if (!_previousThreadStats.TryGetValue(processId, out Dictionary<int, ProcStat>? prevThreadStats))
        {
            prevThreadStats = new Dictionary<int, ProcStat>();
        }

        Dictionary<int, ProcStat> newStats = new (threads.Count);
        foreach (ThreadUsage thread in threads)
        {
            IntPtr threadHandle = WindowsCpuUsageInterop.OpenThread(THREAD_QUERY_INFORMATION, false, (uint) thread.ThreadId);
            try
            {
                if (threadHandle == IntPtr.Zero)
                {
                    continue;
                }

                if (!WindowsCpuUsageInterop.GetThreadTimes(threadHandle, out Filetime _, out Filetime _, out Filetime kernelTime, out Filetime userTime))
                {
                    return Result.CriticalError($"The last Win32 Error was: {Marshal.GetLastWin32Error()}");
                }

                ProcStat currStat = new ()
                {
                    UserTime = userTime.ToLong(),
                    KernelTime = kernelTime.ToLong()
                };

                long delta = 0;
                if (prevThreadStats!.TryGetValue(thread.ThreadId, out ProcStat? prevStat))
                {
                    delta = Calculator.CalculateProcStatDelta(prevStat, currStat);
                }
                double usage = deltaTotalCpu > 0
                    ? Calculator.CalculateCpuPercentFromDeltasNormalized(deltaTotalCpu, delta)
                    : 0;

                thread.CpuUsagePercentNormalized = usage;
                thread.CpuUsagePercent = Calculator.CalculateCpuPercentFromDeltas(usage, _cpuCoresCount);
                newStats[thread.ThreadId] = currStat;

            }
            finally
            {
                WindowsCpuUsageInterop.CloseHandle(threadHandle);
            }
        }

        _previousThreadStats[processId] = newStats;

        return threads;
    }

    private Result<MemorySize> GetProcessMemory(Process process)
    {
        try
        {
            long memBytes = process.PrivateMemorySize64;
            return Result<MemorySize>.Success(MemorySize.FromBytes(memBytes));
        }
        catch (Exception ex)
        {
            return Result<MemorySize>.Error(ex.Message);
        }
    }

    private static Result<ProcStat> GetProcStat(Process process)
    {
        try
        {
            TimeSpan userTime = process.UserProcessorTime;
            TimeSpan kernelTime = process.PrivilegedProcessorTime;
            return Result<ProcStat>.Success(new ProcStat
            {
                UserTime = userTime.Ticks,
                KernelTime = kernelTime.Ticks
            });
        }
        catch (Exception ex)
        {
            return Result<ProcStat>.Error(ex.Message);
        }
    }

    private static Result<long> GetTotalCpuTime()
    {
        try
        {
            if (!WindowsCpuUsageInterop.GetSystemTimes(out Filetime _, out Filetime kernelTime, out Filetime userTime))
            {
                return Result.CriticalError($"The last Win32 Error was: {Marshal.GetLastWin32Error()}");
            }

            long totalTime = kernelTime.ToLong() + userTime.ToLong();
            return totalTime;
        }
        catch (Exception ex)
        {
            return Result.Error(ex.Message);
        }
    }

    private static Result<ProcStat> GetProcStat()
    {
        try
        {
            IntPtr ptr = WindowsCpuUsageInterop.GetCurrentProcess();
            if (!WindowsCpuUsageInterop.GetProcessTimes(ptr, out Filetime _, out Filetime _, out Filetime kernelTime, out Filetime userTime))
            {
                return Result.CriticalError($"The last Win32 Error was: {Marshal.GetLastWin32Error()}");
            }

            ProcStat procStat = new ()
            {
                UserTime = userTime.ToLong(),
                KernelTime = kernelTime.ToLong()
            };

            return procStat;
        }
        catch (Exception ex)
        {
            return Result.Error(ex.Message);
        }
    }

    private static Result<MemorySize> GetProcessMemory()
    {
        Result<ProcessMemoryCounters> result = ProcessMemoryCounters.Create();

        if (!result.IsSuccess)
        {
            return Result.Error(result.Errors.ToErrorList());
        }

        return MemorySize.FromBytes(result.Value.PagefileUsage);
    }

    public void Dispose()
    {

    }
}
