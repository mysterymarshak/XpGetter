using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class HelloState : BaseState
{
    private bool _skipToStart;
    private bool _helloSaid;

    public HelloState(StateContext context, bool skipToStart = false) : base(context)
    {
        _skipToStart = skipToStart;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        StateExecutionResult? lastExecutionResult = null;

        while (true)
        {
            if (lastExecutionResult is ErrorExecutionResult errorExecutionResult)
            {
                AnsiConsole.WriteLine();
                errorExecutionResult.DumpError();
            }

            if (lastExecutionResult is ExitExecutionResult)
            {
                return lastExecutionResult;
            }

            if (!_helloSaid)
            {
                AnsiConsole.MarkupLine(Messages.Start.Hello);
                _helloSaid = true;
            }

            if (lastExecutionResult?.CheckAndPrintAccounts is true or null)
            {
                if (Configuration.Accounts.Any())
                {
                    var savedAccounts = string.Join(
                        ", ",
                        Configuration.Accounts.Select(x => string.Format(
                                                           (string)Messages.Start.SavedAccountFormat,
                                                           (object?)x.GetDisplayUsername())));
                    AnsiConsole.MarkupLine(Messages.Start.SavedAccounts, savedAccounts);
                }
                else
                {
                    AnsiConsole.MarkupLine(Messages.Start.NoAccounts);
                    lastExecutionResult = await GoTo<AddAccountState>();
                    continue;
                }
            }

            if (_skipToStart)
            {
                lastExecutionResult = await GoToStartState();
                continue;
            }

            // TODO: calendar
            var choices = new List<string>
            {
                Messages.Start.GetActivityInfo,
                Messages.Start.Statistics,
                Messages.Start.ManageAccounts,
                Messages.Start.CheckForUpdates,
                Messages.Common.Exit
            };

            var choice = await AnsiConsole.PromptAsync(new SelectionPrompt<string>()
                                                       .Title(Messages.Common.ChoiceOption)
                                                       .AddChoices(choices));

            lastExecutionResult = choice switch
            {
                Messages.Start.GetActivityInfo => await GoToStartState(),
                Messages.Start.Statistics => await GoTo<ChooseStatisticsPeriodState>(),
                Messages.Start.ManageAccounts => await GoTo<ManageAccountsState>(),
                Messages.Start.CheckForUpdates => await GoTo<CheckUpdatesState>(),
                _ => new ExitExecutionResult()
            };
        }
    }

#pragma warning disable CS8974
    private ValueTask<StateExecutionResult> GoToStartState()
    {
        return GoTo<StartState>(new NamedParameter("postAuthenticationDelegate", GetActivityInfoDelegate));
    }
#pragma warning restore CS8974

    private async Task<StateExecutionResult> GetActivityInfoDelegate(List<SteamSession> sessions)
    {
        var retrieveActivityStateResult = await AnsiConsole
            .CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                return (RetrieveActivityExecutionResult)await GoTo<RetrieveActivityState>(
                    new NamedParameter("sessions", sessions),
                    new NamedParameter("ctx", ctx));
            });

        if (retrieveActivityStateResult.ActivityInfos.Any())
        {
            await GoTo<PrintActivityState>(
                new NamedParameter("activityInfos", retrieveActivityStateResult.ActivityInfos));
        }

        return retrieveActivityStateResult;
    }
}
