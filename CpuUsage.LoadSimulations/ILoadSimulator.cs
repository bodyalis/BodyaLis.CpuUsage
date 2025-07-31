namespace CpuUsage.LoadSimulations;

public interface ILoadSimulator
{
    void StartCpuLoad(int numberOfThreads = 1);
    void StartMemoryLoad(int numberOfThreads = 1);
    void StartMonitoring();
}
