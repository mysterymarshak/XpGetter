using XpGetter.Application.Dto;

namespace XpGetter.Cli.States.Results;

public class AuthenticationExecutionResult : StateExecutionResult
{
    public IEnumerable<SteamSession> AuthenticatedSessions { get; init; } = [];
    public ErrorExecutionResult? Error { get; init; }
}