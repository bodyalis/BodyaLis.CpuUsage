using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CpuUsage.Core.Platform.Windows;

internal static class WindowsCpuUsageInterop
{
    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool GlobalMemoryStatusEx([In] [Out] MemoryStatusExternal lpBuffer);


    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool GetSystemTimes(out Filetime idleTime, out Filetime kernelTime, out Filetime userTime);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetThreadTimes(
        IntPtr hThread,
        out Filetime lpCreationTime,
        out Filetime lpExitTime,
        out Filetime lpKernelTime,
        out Filetime lpUserTime);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);
    
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();


    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetProcessTimes(
        IntPtr hThread,
        out Filetime lpCreationTime,
        out Filetime lpExitTime,
        out Filetime lpKernelTime,
        out Filetime lpUserTime);

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool GetProcessMemoryInfo(
        IntPtr hProcess,
        out ProcessMemoryCounters counters,
        uint size);
}
public static class ThreadSnapshot
{
    private const uint TH32CS_SNAPTHREAD = 0x00000004;
    private const uint TH32CS_SNAPMODULE = 0x00000008;


    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32First(IntPtr hSnapshot, ref Threadentry32 lpte);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Thread32Next(IntPtr hSnapshot, ref Threadentry32 lpte);

    public static List<uint> GetThreadIdsOfProcess(int processId)
    {
        List<uint> threadIds = new ();

        IntPtr snapshot = ThreadSnapshot.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            throw new Win32Exception("CreateToolhelp32Snapshot failed");
        }

        try
        {
            Threadentry32 te = new ();
            te.dwSize = (uint) Marshal.SizeOf(typeof (Threadentry32));
            if (!ThreadSnapshot.Thread32First(snapshot, ref te))
            {
                return threadIds;
            }

            do
            {
                if (te.th32OwnerProcessID == (uint) processId)
                {
                    threadIds.Add(te.th32ThreadID);
                }
            } while (ThreadSnapshot.Thread32Next(snapshot, ref te));
        }
        finally
        {
            WindowsCpuUsageInterop.CloseHandle(snapshot);
        }

        return threadIds;
    }
}
[StructLayout(LayoutKind.Sequential)]
public struct Threadentry32
{
    public uint dwSize;
    public uint cntUsage;
    public uint th32ThreadID;
    public uint th32OwnerProcessID;
    public int tpBasePri;
    public int tpDeltaPri;
    public uint dwFlags;
}
