using System.Diagnostics;
using Ardalis.Result;
using CpuUsage.Core;
using CpuUsage.Core.Models;
using CpuUsage.Core.Platform.General;

namespace CpuUsage.LoadSimulations;

public abstract class LoadSimulatorBase : ILoadSimulator, IDisposable
{
    private Timer? _monitoringTimer;
    private readonly IProcessUsageProvider _provider;
    private int MonitoringIntervalMs { get; }
    private int LoadSleepMs { get; }

    private readonly List<Thread> _cpuThreads = new ();
    private readonly List<Thread> _memThreads = new ();

    private CancellationTokenSource? _cpuCts;
    private CancellationTokenSource? _memCts;

    public bool CpuStarted => _cpuThreads.Any(t => t.IsAlive);
    public bool MemoryStarted => _memThreads.Any(t => t.IsAlive);

    protected LoadSimulatorBase(int monitoringIntervalMs = 1000, int loadSleepMs = 40)
    {
        _provider = ProcessUsageProviderFactory.Create();
        MonitoringIntervalMs = monitoringIntervalMs;
        LoadSleepMs = loadSleepMs;
    }

    /// <summary> Запуск CPU нагрузки </summary>
    public virtual void StartCpuLoad(int numberOfThreads = 1)
    {
        if (CpuStarted)
        {
            return; // Уже запущены
        }


        _cpuCts = new CancellationTokenSource();
        for (int i = 0; i < numberOfThreads; i++)
        {
            Thread t = new (() => CpuLoadSimulation(_cpuCts.Token)) { IsBackground = true };
            _cpuThreads.Add(t);
            t.Start();
        }
    }

    /// <summary> Остановка CPU потоков </summary>
    public void StopCpuLoad()
    {
        _cpuCts?.Cancel();
        foreach (Thread t in _cpuThreads)
        {
            if (t.IsAlive)
            {
                t.Join(500);
            }
        }
        _cpuThreads.Clear();
        _cpuCts?.Dispose();
        _cpuCts = null;
    }

    /// <summary> Запуск памяти нагрузки </summary>
    public virtual void StartMemoryLoad(int numberOfThreads = 1)
    {
        if (MemoryStarted)
        {
            return; // Уже запущены
        }

        _memCts = new CancellationTokenSource();
        for (int i = 0; i < numberOfThreads; i++)
        {
            Thread t = new (() => MemoryLoadSimulation(_memCts.Token)) { IsBackground = true };
            _memThreads.Add(t);
            t.Start();
        }
    }

    /// <summary> Остановка памяти потоков </summary>
    public void StopMemoryLoad()
    {
        _memCts?.Cancel();
        foreach (Thread t in _memThreads)
        {
            if (t.IsAlive)
            {
                t.Join(500);
            }
        }
        _memThreads.Clear();
        _memCts?.Dispose();
        _memCts = null;
    }

    /// <summary> Запуск мониторинга с таймером </summary>
    public virtual void StartMonitoring()
    {
        _monitoringTimer = new Timer(MonitoringTimerCallback, null, 0, MonitoringIntervalMs);
    }

    protected void MonitoringTimerCallback(object? state)
    {
        Console.Clear();
        Stopwatch sw = Stopwatch.StartNew();
        Result<ProcessUsage> result = _provider.GetCurrentProcessUsage();
        sw.Stop();
        long i = sw.ElapsedMilliseconds;
        Console.WriteLine($"Monitoring took {i} milliseconds");

        lock (Console.Out)
        {
            if (result.IsSuccess && result.Value != null)
            {
                PrintProcessUsage(result.Value);
            }
            else
            {
                Console.WriteLine("Monitoring error: " + string.Join(", ", result.Errors));
            }
        }
    }

    protected void PrintProcessUsage(ProcessUsage usage, string indent = "")
    {
        Console.WriteLine($"{indent}PID: {usage.ProcessId}");
        Console.WriteLine($"{indent}  CPU UsageNormalized: {usage.CpuUsagePercentNormalized:F2}%");
        Console.WriteLine($"{indent}  CPU Usage: {usage.CpuUsagePercent:F2}%");
        Console.WriteLine($"{indent}  Memory   : {usage.MemoryBytes.Megabytes:F3} MB ({usage.MemoryPercent:F2}%)");

        if (usage.Threads.Count > 0)
        {
            Console.WriteLine($"{indent}  Threads: {usage.Threads.Count}");
            foreach (ThreadUsage thread in usage.Threads)
            {
                Console.WriteLine($"{indent}    TID: {thread.ThreadId:000000}       CPUNorm: {thread.CpuUsagePercentNormalized:00.00}       CPU: {thread.CpuUsagePercent:000.00}");
            }
        }

        if (usage.Children.Count > 0)
        {
            Console.WriteLine($"{indent}  Child Processes:");
            foreach (ProcessUsage child in usage.Children)
            {
                PrintProcessUsage(child, indent + "    ");
            }
        }
    }

    protected void CpuLoadSimulation(CancellationToken cancel)
    {
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                for (int i = 1; i <= 10_000_000 && !cancel.IsCancellationRequested; i++)
                {
                    IntensiveFactorial(i);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Поток завершился ожидаемо
        }
    }

    private void IntensiveFactorial(int n)
    {
        long fact = 1;
        for (int j = 2; j <= Math.Min(n, 20); j++)
        {
            fact *= j;
        }
    }

    protected void MemoryLoadSimulation(CancellationToken cancel)
    {
        List<byte[]> allocatedBlocks = new ();
        Random rnd = new ();
        int iteration = 0;

        try
        {
            while (!cancel.IsCancellationRequested)
            {
                Thread.Sleep(LoadSleepMs);

                int blockSize = rnd.Next(10_000_000, 500_000_000);
                byte[] block = new byte[blockSize];
                rnd.NextBytes(block);

                allocatedBlocks.Add(block);
                iteration++;

                if (iteration % 50 == 0)
                {
                    int removeCount = allocatedBlocks.Count / 2;
                    allocatedBlocks.RemoveRange(0, removeCount);
                    GC.Collect();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Поток завершился ожидаемо
        }
    }

    public virtual void Dispose()
    {
        _monitoringTimer?.Dispose();
        StopCpuLoad();
        StopMemoryLoad();
    }
}
