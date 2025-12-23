using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.Extensions;

public static class ErrorExecutionResultExtensions
{
    extension(ErrorExecutionResult? source)
    {
        public ErrorExecutionResult CombineOrCreate(ErrorExecutionResult other)
        {
            if (source is null)
            {
                return other;
            }

            return new ErrorExecutionResult(source.DumpErrorDelegate + other.DumpErrorDelegate);
        }
    }
}
