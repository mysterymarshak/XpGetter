namespace XpGetter.Cli.States.Results;

public class PanicExecutionResult : ErrorExecutionResult
{
    public PanicExecutionResult()
    {
    }

    public PanicExecutionResult(string? message = null)
    {
        Message = message;
    }

    public PanicExecutionResult(Action? dumpErrorDelegate = null) : base(dumpErrorDelegate)
    {
    }
}
