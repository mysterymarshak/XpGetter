namespace XpGetter.Cli.States.Results;

public class ErrorExecutionResult : StateExecutionResult
{
    public Action? DumpErrorDelegate { get; init; }

    public ErrorExecutionResult(Action? dumpErrorDelegate = null)
    {
        DumpErrorDelegate = dumpErrorDelegate;
    }

    public void DumpError()
    {
        DumpErrorDelegate?.Invoke();
    }
}