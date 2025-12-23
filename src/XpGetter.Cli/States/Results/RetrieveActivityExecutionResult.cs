using XpGetter.Application.Dto;

namespace XpGetter.Cli.States.Results;

public class RetrieveActivityExecutionResult : StateExecutionResult
{
    public IEnumerable<ActivityInfo> ActivityInfos { get; init; } = [];
    public ErrorExecutionResult? Error { get; init; }
}