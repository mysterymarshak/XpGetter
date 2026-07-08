using XpGetter.Application.Features.Cs2;

namespace XpGetter.Tui.States.Results;

public class RetrieveWeeklyDropExecutionResult : StateExecutionResult
{
    public List<Cs2InventoryItem> AvailableItems { get; init; } = [];
    public Cs2Client? Cs2Client { get; init; }
}
