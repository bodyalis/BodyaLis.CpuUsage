using Ardalis.Result;

namespace CpuUsage.Core.Platform.General;

public static class EnumerableExt
{
    public static ErrorList ToErrorList(this IEnumerable<string> arr) => new (arr);
}
public static class ErrorListExtensions
{
    public static string ToString(this ErrorList errList) => string.Join(Environment.NewLine, errList.ErrorMessages.Select(x => x.ToString()));
}
