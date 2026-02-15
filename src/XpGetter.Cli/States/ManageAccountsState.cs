using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class ManageAccountsState : BaseState
{
    public ManageAccountsState(StateContext context) : base(context)
    {
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var usernames = Configuration.Accounts
            .Select(x => x.Username);

        var choicesEnumerable = Configuration.Accounts
            .Select(x => x.GetDisplayUsername());

        if (Configuration.Accounts.Count() < Constants.MaxAccounts)
        {
            choicesEnumerable = choicesEnumerable.Append(Messages.ManageAccounts.AddNew);
        }

        var choices = choicesEnumerable
            .Append(Messages.Common.Back)
            .Append(Messages.Common.Exit)
            .ToList();

        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(Messages.Common.ChoiceOption)
                .AddChoices(choices));

        var backOption = () => GoTo<ManageAccountsState>();

        return choice switch
        {
            Messages.ManageAccounts.AddNew => await GoTo<AddAccountState>(new NamedParameter("backOption", backOption)),
            Messages.Common.Back => SuccessExecutionResult.WithoutAccountsPrint(),
            Messages.Common.Exit => new ExitExecutionResult(),
            _ when choices.IndexOf(choice) is var index and >= 0 => await GoTo<ManageAccountState>(
                new NamedParameter("username", usernames.ElementAt(index))),
            _ => throw new ArgumentOutOfRangeException(nameof(choice))
        };
    }
}
