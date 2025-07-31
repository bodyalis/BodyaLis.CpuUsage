using CpuUsage.Core.Models;


namespace CpuUsage.Core.Platform.General;

public static class Calculator
{
    public static long CalculateProcStatDelta(ProcStat previousStat, ProcStat nextStat)
    {
        if (nextStat.KernelTime <= 0 || previousStat.KernelTime <= 0)
        {
            return nextStat.UserTime + nextStat.KernelTime - (previousStat.UserTime + previousStat.KernelTime);
        }

        long k = nextStat.UserTime + nextStat.KernelTime - (previousStat.UserTime + previousStat.KernelTime);
        return k;

    }

    public static double CalculateCpuPercentFromDeltasNormalized(long deltaTotalCpu, long deltaProcCpu) => deltaProcCpu / (double) deltaTotalCpu * 100;

    public static double CalculateCpuPercentFromDeltas(double normalizedPercent, int cpuCoreCount) => normalizedPercent * cpuCoreCount;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="totalCpuPercent">Вычисленное отношение usertime/cputime </param>
    /// <param name="cpuCoreCount"></param>
    /// <returns></returns>
    public static double ConvertTotalCpuPercentToAveragePerCore(double totalCpuPercent, int cpuCoreCount)
    {
        double result = totalCpuPercent / cpuCoreCount;
        return result < 0 ? 0 : result;
    }

    public static double CalculateMemoryPercent(MemorySize totalSize, MemorySize processSize)
        => processSize / totalSize * 100;
}
