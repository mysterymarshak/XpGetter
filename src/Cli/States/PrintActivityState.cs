using Spectre.Console;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;
using XpGetter.Utils;

namespace XpGetter.Cli.States;

public class PrintActivityState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly IEnumerable<ActivityInfo> _activityInfos;

    public PrintActivityState(AppConfigurationDto configuration, IEnumerable<ActivityInfo> activityInfos,
        StateContext context) : base(context)
    {
        _configuration = configuration;
        _activityInfos = activityInfos.ToList();
    }

    public override ValueTask<StateExecutionResult> OnExecuted()
    {
        PrintActivityInfo();
        return ValueTask.FromResult<StateExecutionResult>(new SuccessExecutionResult());
    }

    private void PrintActivityInfo()
    {
        foreach (var (i, info) in _activityInfos.Index())
        {
            var newRankDrop = info.LastNewRankDrop;
            var xpData = info.XpData;
            var isDropAvailable = newRankDrop.IsDropAvailable();
            var isDropAvailableFormatted =
                isDropAvailable is null ? "<unknown>" : (isDropAvailable.Value ? "Yes" : "No");
            var dropAvailableColor = isDropAvailable is null ? "white" : (isDropAvailable.Value ? "green" : "red");
            AnsiConsole.MarkupLine($"[blue]{info.Account.PersonalName}[/]");
            AnsiConsole.MarkupLine($"Rank: {xpData.Rank}");
            AnsiConsole.MarkupLine($"Last drop time: {info.LastNewRankDrop.GetLastDropTime()}");
            AnsiConsole.MarkupLine($"Last loot: {newRankDrop.GetPreviousLoot()}");
            AnsiConsole.MarkupLine($"Drop is available: [{dropAvailableColor}]{isDropAvailableFormatted}[/]");
            if (info.AdditionalMessage is not null)
            {
                AnsiConsole.MarkupLine($"[yellow]{info.AdditionalMessage}[/]");
            }
            // TODO: formatting

            ProgressBar.Print(xpData.Xp, 5000);

            if (i != _activityInfos.Count() - 1)
            {
                Console.WriteLine();
            }
        }
    }
}
