using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Extensions;

public static class ErrorExecutionResultExtensions
{
    extension(ErrorExecutionResult result)
    {
        public ErrorExecutionResult Combine(ErrorExecutionResult other)
        {
            return new ErrorExecutionResult(result.DumpErrorDelegate + other.DumpErrorDelegate);
        }
    }
}