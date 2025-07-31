using System.Runtime.InteropServices;
using CpuUsage.Core.Platform.General;
using CpuUsage.Core.Platform.Linux;
using CpuUsage.Core.Platform.Windows;

namespace CpuUsage.Core;

public static class ProcessUsageProviderFactory
{
    public static IProcessUsageProvider Create()
    {
        OperatingSystem.IsLinux();
        if (OperatingSystem.IsLinux())
        {
            return new LinuxProcessUsageProvider();
        }
        if (OperatingSystem.IsWindows())
        {
            return new WindowsProcessUsageProvider();
        }
        throw new PlatformNotSupportedException();
    }
}
