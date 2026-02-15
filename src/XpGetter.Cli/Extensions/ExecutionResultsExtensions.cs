using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.Extensions;

public static class ErrorExecutionResultExtensions
{
    extension(ErrorExecutionResult? source)
    {
        public ErrorExecutionResult? CombineOrCreate(ErrorExecutionResult? other)
        {
            if (source is null)
            {
                return other;
            }

            if (other is null)
            {
                return source;
            }

            return new ErrorExecutionResult(source.DumpErrorDelegate + other.DumpErrorDelegate);
        }
    }
}

public static class SuccessExecutionResultExtensions
{
    extension(SuccessExecutionResult)
    {
        public static SuccessExecutionResult WithoutAccountsPrint() =>
            new SuccessExecutionResult { CheckAndPrintAccounts = false };
    }
}
