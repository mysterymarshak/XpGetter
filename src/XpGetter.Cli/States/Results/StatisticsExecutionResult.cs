using XpGetter.Application.Dto;

namespace XpGetter.Cli.States.Results;

public class StatisticsExecutionResult : StateExecutionResult
{
    public List<StatisticsDto> Statistics { get; init; } = [];
}
