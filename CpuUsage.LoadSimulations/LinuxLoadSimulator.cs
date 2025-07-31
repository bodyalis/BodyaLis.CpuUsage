namespace CpuUsage.LoadSimulations;

public class LinuxLoadSimulator(int monitoringIntervalMs = 1000, int loadSleepMs = 40)
    : LoadSimulatorBase(monitoringIntervalMs, loadSleepMs);
