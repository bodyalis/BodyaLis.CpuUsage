using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Xml;
using Ardalis.Result;

namespace CpuUsage.Core.Platform.Windows;

[StructLayout(LayoutKind.Sequential)]
public struct ProcessMemoryCounters
{
    public uint cb;
    public uint PageFaultCount;
    public ulong PeakWorkingSetSize;
    public ulong WorkingSetSize;
    public ulong QuotaPeakPagedPoolUsage;
    public ulong QuotaPagedPoolUsage;
    public ulong QuotaPeakNonPagedPoolUsage;
    public ulong QuotaNonPagedPoolUsage;
    public ulong PagefileUsage;
    public ulong PeakPagefileUsage;

    public static Result<ProcessMemoryCounters> Create()
    {
        IntPtr process = WindowsCpuUsageInterop.GetCurrentProcess();
        bool result = WindowsCpuUsageInterop.GetProcessMemoryInfo(process, out ProcessMemoryCounters pmcounters, (uint) Marshal.SizeOf<ProcessMemoryCounters>());
        return result ? pmcounters : Result.CriticalError($"The last Win32 Error was: {Marshal.GetLastWin32Error()}");
    }
}
