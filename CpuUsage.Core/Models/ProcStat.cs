namespace CpuUsage.Core.Models;

public class ProcStat
{
    public string Comm = "";
    public int Pid;
    public int PPid;
    public string State = "";
    public long KernelTime;
    public long UserTime;
}
