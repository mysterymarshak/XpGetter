using XpGetter.Application.Dto;

namespace XpGetter.Tui.States.Results;

public class AuthenticationExecutionResult : StateExecutionResult
{
    public List<SteamSession> AuthenticatedSessions { get; init; } = [];
}
