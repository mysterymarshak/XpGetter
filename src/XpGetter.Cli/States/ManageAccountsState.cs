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
        var choices = _configuration.Accounts
            .Select(x => x.Username);

        if (_configuration.Accounts.Count() < Constants.MaxAccounts)
        {
            choices = choices.Append(Messages.ManageAccounts.AddNew);
        }

        choices = choices
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
            _ => await GoTo<ManageAccountState>(
                new NamedParameter("username", choice), new NamedParameter("configuration", _configuration))
        };
    }
}
