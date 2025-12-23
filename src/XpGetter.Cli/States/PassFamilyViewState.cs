using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class PassFamilyViewState : BaseState
{
    private readonly List<SteamSession> _sessions;

    public PassFamilyViewState(List<SteamSession> sessions, StateContext context) : base(context)
    {
        _sessions = sessions;
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

            AnsiConsole.MarkupLine(Messages.Parental.AccountIsLocked, session.Name);
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

            var unlockResult = (UnlockFamilyViewExecutionResult)await GoTo<UnlockFamilyViewState>(
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

            AnsiConsole.MarkupLine(Messages.Parental.SkipUnlocking, session.Name);
        }

        return new PassFamilyViewExecutionResult { PassedSessions = passedSession, Error =  errorExecutionResult };
    }
}
