using XpGetter.Application.Dto;

namespace XpGetter.Cli.States.Results;

public class RetrieveActivityStateResult : StateExecutionResult
{
    public IEnumerable<ActivityInfo> ActivityInfos { get; init; } = [];
    public ErrorExecutionResult? Error { get; init; }
}