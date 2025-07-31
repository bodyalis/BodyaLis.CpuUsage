using System.Runtime.InteropServices;

namespace CpuUsage.Core.Platform.Windows;

[StructLayout(LayoutKind.Sequential)]
public struct Filetime
{
    public uint dwLowDateTime;
    public uint dwHighDateTime;

    public long ToLong() => ((long) dwHighDateTime << 32) + dwLowDateTime;
}
