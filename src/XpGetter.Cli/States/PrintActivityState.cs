using Spectre.Console;
using XpGetter.Application.Dto;
using XpGetter.Cli.States.Results;
using XpGetter.Cli.Utils;

namespace XpGetter.Cli.States;

public class PrintActivityState : BaseState
{
    private readonly IEnumerable<ActivityInfo> _activityInfos;

    public PrintActivityState(IEnumerable<ActivityInfo> activityInfos, StateContext context) : base(context)
    {
        _activityInfos = activityInfos.ToList();
    }

    public override ValueTask<StateExecutionResult> OnExecuted()
    {
        // TODO: formatting

        foreach (var (i, info) in _activityInfos.Index())
        {
            var newRankDrop = info.LastNewRankDrop;
            var xpData = info.XpData;
            var isDropAvailable = newRankDrop.IsDropAvailable();
            var isDropAvailableFormatted =
                isDropAvailable is null ? "<unknown>" : (isDropAvailable.Value ? "Yes" : "No");
            var dropAvailableColor = isDropAvailable is null ? "white" : (isDropAvailable.Value ? "green" : "red");
            AnsiConsole.MarkupLine(
                $"[blue][link=https://steamcommunity.com/profiles/{info.Account.Id}]{info.Account.GetDisplayPersonalName()}[/][/]");
            AnsiConsole.MarkupLine($"Rank: {xpData.Rank}");
            AnsiConsole.MarkupLine($"Last drop time: {info.LastNewRankDrop.GetLastDropTime()}");
            AnsiConsole.MarkupLine($"Last drop: {newRankDrop.GetPreviousLoot()}");
            AnsiConsole.MarkupLine($"Drop is available: [{dropAvailableColor}]{isDropAvailableFormatted}[/]");
            if (info.AdditionalMessage is not null)
            {
                AnsiConsole.MarkupLine($"[yellow]{info.AdditionalMessage}[/]");
            }
            ProgressBar.Print(xpData.Xp, 5000);

            if (i != _activityInfos.Count() - 1)
            {
                AnsiConsole.WriteLine();
            }
        }

        return ValueTask.FromResult<StateExecutionResult>(new SuccessExecutionResult());
    }
}
