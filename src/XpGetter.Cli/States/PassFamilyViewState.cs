using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class PassFamilyViewState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly List<SteamSession> _sessions;
    private readonly IConfigurationService _configurationService;

    public PassFamilyViewState(AppConfigurationDto configuration, List<SteamSession> sessions,
                               IConfigurationService configurationService, StateContext context) : base(context)
    {
        _configuration = configuration;
        _sessions = sessions;
        _configurationService = configurationService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        ErrorExecutionResult? errorExecutionResult = null;
        var passedSession = new List<SteamSession>(_sessions.Count);

        foreach (var session in _sessions)
        {
            var parentalSettings = session.ParentalSettings;
            if (parentalSettings?.is_enabled != true)
            {
                var account = session.Account!;
                if (!string.IsNullOrWhiteSpace(account.FamilyViewPin))
                {
                    account.FamilyViewPin = null;
                }
                passedSession.Add(session);
                continue;
            }

            var featureFlags = parentalSettings.enabled_features;
            if ((featureFlags & 0b1000) != 0) // (Profile, ..)
            {
                if ((featureFlags & 0b100000000000) != 0 || // All games
                    parentalSettings.applist_custom.FirstOrDefault(x => x.appid == 730)?.is_allowed == true) // or csgo allowed
                {
                    passedSession.Add(session);
                    continue;
                }

                // TODO: could skip but no inventory history
            }

            AnsiConsole.MarkupLine(Messages.Parental.AccountIsLocked, session.GetName());

            if (!string.IsNullOrWhiteSpace(session.Account!.FamilyViewPin))
            {
                AnsiConsole.MarkupLine(Messages.Parental.FoundSavedPin);
            }
            else
            {
                var prompt = new TextPrompt<string>(Messages.Parental.PromptUnlocking)
                    .AddChoice(Messages.Common.Y)
                    .AddChoice(Messages.Common.N)
                    .DefaultValue(Messages.Common.Y);

                var promptResult = await AnsiConsole.PromptAsync(prompt);
                if (promptResult != Messages.Common.Y)
                {
                    AnsiConsole.MarkupLine(Messages.Parental.SkipUnlocking);
                    continue;
                }
            }

            var unlockResult = (UnlockFamilyViewExecutionResult)await GoTo<UnlockFamilyViewState>(
                new NamedParameter("configuration", _configuration),
                new NamedParameter("session", session));

            if (unlockResult.Success)
            {
                passedSession.Add(session);
                continue;
            }

            if (unlockResult.Error is not null)
            {
                AnsiConsole.MarkupLine(Messages.Parental.SkipUnlockingDueToError);
                errorExecutionResult = errorExecutionResult.CombineOrCreate(unlockResult.Error);
                continue;
            }

            AnsiConsole.MarkupLine(Messages.Parental.SkipUnlocking, session.GetName());
        }

        _configurationService.WriteConfiguration(_configuration);
        return new PassFamilyViewExecutionResult { PassedSessions = passedSession, Error =  errorExecutionResult };
    }
}
