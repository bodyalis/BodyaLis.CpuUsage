using BenchmarkDotNet.Running;

namespace CpuUsage.Benchmark;

internal class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<CpuUsageCoreBenchmark>();
    }
}
