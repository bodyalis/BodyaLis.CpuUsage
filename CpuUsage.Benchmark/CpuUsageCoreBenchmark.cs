using BenchmarkDotNet.Attributes;
using CpuUsage.Core;
using CpuUsage.Core.Platform.General;
using CpuUsage.Core.Platform.Linux;

namespace CpuUsage.Benchmark;

[RankColumn]
[MemoryDiagnoser]
public class CpuUsageCoreBenchmark
{
    private IProcessUsageProvider _provider;

    [GlobalSetup]
    public void Setup()
    {
        _provider = ProcessUsageProviderFactory.Create();

        _provider.GetCurrentProcessUsage();
    }

    [Benchmark]
    public void MeasureCurrentProcessUsage()
    {
        _provider.GetCurrentProcessUsage();
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();
}
