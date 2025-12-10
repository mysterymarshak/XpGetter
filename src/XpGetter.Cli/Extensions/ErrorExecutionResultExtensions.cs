using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.Extensions;

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