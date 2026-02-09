using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class ManageAccountsState : BaseState
{
    private readonly AppConfigurationDto _configuration;

    public ManageAccountsState(AppConfigurationDto configuration, StateContext context) : base(context)
    {
        _configuration = configuration;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var usernames = _configuration.Accounts
            .Select(x => x.Username);

        var choicesEnumerable = _configuration.Accounts
            .Select(x => x.GetDisplayUsername());

        if (_configuration.Accounts.Count() < Constants.MaxAccounts)
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

        var backOption = () => GoTo<ManageAccountsState>(new NamedParameter("configuration", _configuration));

        return choice switch
        {
            Messages.ManageAccounts.AddNew => await GoTo<AddAccountState>(new NamedParameter("backOption", backOption)),
            Messages.Common.Back => await GoTo<HelloState>(
                new NamedParameter("configuration", _configuration),
                new NamedParameter("checkAndPrintAccounts", false),
                new NamedParameter("skipHelloMessage", true)),
            Messages.Common.Exit => new ExitExecutionResult(),
            _ when choices.IndexOf(choice) is var index and > 0 => await GoTo<ManageAccountState>(
                new NamedParameter("username", usernames.ElementAt(index)),
                    new NamedParameter("configuration", _configuration)),
            _ => throw new ArgumentOutOfRangeException(nameof(choice))
        };
    }
}
