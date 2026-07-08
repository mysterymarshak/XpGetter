namespace XpGetter.Tui.States.Results;

public abstract class StateExecutionResult
{
    public string? Message { get; init; }
    public ErrorExecutionResult? Error { get; init; }
    public bool CheckAndPrintAccounts { get; init; } = true;
    public bool WaitAnyKey { get; init; } = true;
}
