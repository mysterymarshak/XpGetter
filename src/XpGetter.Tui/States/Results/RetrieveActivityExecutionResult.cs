using XpGetter.Application.Dto;

namespace XpGetter.Tui.States.Results;

public class RetrieveActivityExecutionResult : StateExecutionResult
{
    public List<ActivityInfo> ActivityInfos { get; init; } = [];
}
