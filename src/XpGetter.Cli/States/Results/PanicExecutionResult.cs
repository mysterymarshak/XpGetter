namespace XpGetter.Cli.States.Results;

public class PanicExecutionResult : ErrorExecutionResult
{
    public PanicExecutionResult(string? message = null)
    {
        Message = message;
    }
}