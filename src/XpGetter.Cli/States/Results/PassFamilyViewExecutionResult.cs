using XpGetter.Application.Dto;

namespace XpGetter.Cli.States.Results;

public class PassFamilyViewExecutionResult : StateExecutionResult
{
    public List<SteamSession> PassedSessions { get; init; } = [];
    public ErrorExecutionResult? Error { get; init; }
}