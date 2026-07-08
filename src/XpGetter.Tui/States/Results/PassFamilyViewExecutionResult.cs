using XpGetter.Application.Dto;

namespace XpGetter.Tui.States.Results;

public class PassFamilyViewExecutionResult : StateExecutionResult
{
    public List<SteamSession> PassedSessions { get; init; } = [];
}
