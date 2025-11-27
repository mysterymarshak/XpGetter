using XpGetter.Dto;

namespace XpGetter.Results.StateExecutionResults;

public class AuthenticationExecutionResult : StateExecutionResult
{
    public IEnumerable<SteamSession> AuthenticatedSessions { get; init; } = [];
    public ErrorExecutionResult? Error { get; init; }
}