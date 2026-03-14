using XpGetter.Application.Features.Cs2;

namespace XpGetter.Cli.States.Results;

public class ChooseWeeklyDropToClaimExecutionResult : StateExecutionResult
{
    public List<Cs2InventoryItem>? SelectedItems { get; init; }
}