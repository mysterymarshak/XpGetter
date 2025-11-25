using Autofac;
using Spectre.Console;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Ui.States;

public class ManageAccountsState : BaseState
{
    private readonly AppConfigurationDto _configuration;

    public ManageAccountsState(AppConfigurationDto configuration, StateContext context) : base(context)
    {
        _configuration = configuration;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        const string addNewChoice = "Add new";
        const string backChoice = "Back";
        const string exitChoice = "Exit";

        var choices = _configuration.Accounts
            .Select(x => x.Username)
            .Append(addNewChoice)
            .Append(backChoice)
            .Append(exitChoice)
            .ToList();

        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Choice option:")
                .AddChoices(choices));

        var backOption = () => GoTo<ManageAccountsState>(new NamedParameter("configuration", _configuration));

        return choice switch
        {
            addNewChoice => await GoTo<AddAccountState>(new NamedParameter("backOption", backOption)),
            backChoice => await GoTo<HelloState>(
                new NamedParameter("configuration", _configuration), new NamedParameter("skipHelloMessage", true)),
            exitChoice => new SuccessExecutionResult(),
            _ => await GoTo<ManageAccountState>(
                new NamedParameter("username", choice), new NamedParameter("configuration", _configuration))
        };
    }
}