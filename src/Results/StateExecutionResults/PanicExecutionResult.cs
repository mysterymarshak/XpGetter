namespace XpGetter.Results.StateExecutionResults;

public class PanicExecutionResult : ErrorExecutionResult
{
    public PanicExecutionResult(string? message = null)
    {
        Message = message;
    }
}