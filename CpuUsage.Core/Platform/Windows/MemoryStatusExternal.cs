using System.Runtime.InteropServices;
using Ardalis.Result;

namespace CpuUsage.Core.Platform.Windows;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public class MemoryStatusExternal
{
    public uint dwLength = (uint) Marshal.SizeOf(typeof (MemoryStatusExternal));
    public uint dwMemoryLoad;
    public ulong ullTotalPhys;
    public ulong ullAvailPhys;
    public ulong ullTotalPageFile;
    public ulong ullAvailPageFile;
    public ulong ullTotalVirtual;
    public ulong ullAvailVirtual;
    public ulong ullAvailExtendedVirtual;

    private MemoryStatusExternal()
    {
    }

    public static Result<MemoryStatusExternal> Create()
    {
        MemoryStatusExternal memoryStatusExternal = new ();
        bool result = WindowsCpuUsageInterop.GlobalMemoryStatusEx(memoryStatusExternal);
        return result ? memoryStatusExternal : Result.CriticalError($"The last Win32 Error was: {Marshal.GetLastWin32Error()}");
    }
}
