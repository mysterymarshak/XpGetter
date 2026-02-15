namespace XpGetter.Cli.States.Results;

public abstract class StateExecutionResult
{
    public string? Message { get; init; }
    public ErrorExecutionResult? Error { get; init; }
    public bool CheckAndPrintAccounts { get; init; } = true;
}
