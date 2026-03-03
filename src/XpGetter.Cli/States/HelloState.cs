using Spectre.Console;
using XpGetter.Application;
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
                errorExecutionResult.DumpError();
                AnsiConsole.WriteLine();
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
                                                           x.GetDisplayUsername())));
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
                lastExecutionResult = await GoTo<RetrieveActivityState>();
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
                Messages.Start.GetActivityInfo => await GoTo<RetrieveActivityState>(),
                Messages.Start.Statistics => await GoTo<ChooseStatisticsPeriodState>(),
                Messages.Start.ManageAccounts => await GoTo<ManageAccountsState>(),
                Messages.Start.CheckForUpdates => await GoTo<CheckUpdatesState>(),
                _ => new ExitExecutionResult()
            };
        }
    }
}
