using Ardalis.Result;
using CpuUsage.Core.Models;

namespace CpuUsage.Core.Platform.General;

public interface IProcessUsageProvider : IDisposable
{
    Result<ProcessUsage> GetCurrentProcessUsage(bool includeChildren = true, bool includeThreads = true);
}
