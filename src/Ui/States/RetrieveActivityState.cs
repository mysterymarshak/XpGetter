using Spectre.Console;
using XpGetter.Dto;
using XpGetter.Extensions;
using XpGetter.Results.StateExecutionResults;
using XpGetter.Steam.Services;
using XpGetter.Utils;

namespace XpGetter.Ui.States;

public class RetrieveActivityState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly List<SteamSession> _sessions;
    private readonly IActivityService _activityService;

    public RetrieveActivityState(AppConfigurationDto configuration, List<SteamSession> sessions,
        IActivityService activityService, StateContext context) : base(context)
    {
        _configuration = configuration;
        _sessions = sessions;
        _activityService = activityService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var tasks = _sessions.Select(_activityService.GetActivityInfoAsync);
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            if (result.TryPickT1(out var error, out _))
            {
                error.DumpToConsole(Messages.Activity.GetActivityError);
            }
        }

        var successResults = results
            .Where(x => x.IsT0)
            .Select(x => x.AsT0)
            .ToList();

        PrintActivityInfo(successResults);

        return new SuccessExecutionResult();
    }

    private void PrintActivityInfo(List<ActivityInfo> activityInfos)
    {
        foreach (var (i, info) in activityInfos.Index())
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

            if (i != activityInfos.Count - 1)
            {
                Console.WriteLine();
            }
        }
    }
}
