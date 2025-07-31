namespace CpuUsage.Core.Models;

public class ThreadUsage
{
    public int ThreadId { get; set; }
    public double CpuUsagePercentNormalized { get; set; }
    public double CpuUsagePercent { get; set; }
}
