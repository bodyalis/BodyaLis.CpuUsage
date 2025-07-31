namespace CpuUsage.Core.Models;

public record ProcessUsage
{
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public double CpuUsagePercentNormalized { get; set; }
    public double CpuUsagePercent { get; set; }
    public MemorySize MemoryBytes { get; set; }

    public double MemoryPercent { get; set; }
    public List<ProcessUsage> Children { get; set; } = [];
    public List<ThreadUsage> Threads { get; set; } = [];
}
