using XpGetter.Application.Dto;

namespace XpGetter.Cli.States.Results;

public class AuthenticationExecutionResult : StateExecutionResult
{
    public List<SteamSession> AuthenticatedSessions { get; init; } = [];
    public ErrorExecutionResult? Error { get; init; }
}