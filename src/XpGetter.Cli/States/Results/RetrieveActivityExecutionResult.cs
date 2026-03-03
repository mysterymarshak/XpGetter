using XpGetter.Application.Dto;

namespace XpGetter.Cli.States.Results;

public class RetrieveActivityExecutionResult : StateExecutionResult
{
    public List<ActivityInfo> ActivityInfos { get; init; } = [];
}
