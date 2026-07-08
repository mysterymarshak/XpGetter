using XpGetter.Application.Dto;

namespace XpGetter.Tui.States.Results;

public class StatisticsExecutionResult : StateExecutionResult
{
    public List<StatisticsDto> Statistics { get; init; } = [];
}
