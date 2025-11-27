using XpGetter.Dto;

namespace XpGetter.Results.StateExecutionResults;

public class RetrieveActivityStateResult : StateExecutionResult
{
    public IEnumerable<ActivityInfo> ActivityInfos { get; init; } = [];
    public ErrorExecutionResult? Error { get; init; }
}