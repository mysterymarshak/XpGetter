using XpGetter.Application.Dto;

namespace XpGetter.Cli.States.Results;

public class SuccessExecutionResult : StateExecutionResult
{
    public IEnumerable<ActivityInfo> ActivityInfos { get; init; } = [];
}