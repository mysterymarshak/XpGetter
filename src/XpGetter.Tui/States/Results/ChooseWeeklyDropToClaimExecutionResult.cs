using XpGetter.Application.Features.Cs2;

namespace XpGetter.Tui.States.Results;

public class ChooseWeeklyDropToClaimExecutionResult : StateExecutionResult
{
    public List<Cs2InventoryItem>? SelectedItems { get; init; }
}
