using System.Runtime.InteropServices;

namespace CpuUsage.LoadSimulations;

public class Program
{
    public static void Main()
    {

        ILoadSimulator loadSimulator = LoadSimulatorFactory.Create(1000, 1);
        loadSimulator.StartMemoryLoad(1);
        loadSimulator.StartCpuLoad(1);
        loadSimulator.StartMonitoring();

        Console.ReadLine();

    }
}
public static class LoadSimulatorFactory
{
    public static ILoadSimulator Create(int monitoringInterval, int sleepInterval)
    {
        if (OperatingSystem.IsLinux())
        {
            return new LinuxLoadSimulator(monitoringInterval, sleepInterval);
        }
        if (OperatingSystem.IsWindows())
        {
            return new WindowsLoadSimulator(monitoringInterval, sleepInterval);
        }
        throw new PlatformNotSupportedException();
    }
}
