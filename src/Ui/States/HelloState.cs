using Autofac;
using Spectre.Console;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Ui.States;

public class HelloState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly bool _skipHelloMessage;

    public HelloState(AppConfigurationDto configuration, StateContext context,
        bool skipHelloMessage = false) : base(context)
    {
        _configuration = configuration;
        _skipHelloMessage = skipHelloMessage;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        if (!_skipHelloMessage)
        {
            AnsiConsole.MarkupLine(Messages.Start.Hello);

            if (_configuration.Accounts.Any())
            {
                AnsiConsole.MarkupLine(Messages.Start.SavedAccounts,
                    string.Join(", ", _configuration.Accounts.Select(x =>
                        string.Format(Messages.Start.SavedAccountFormat, x.Username))));
            }
            else
            {
                AnsiConsole.MarkupLine(Messages.Start.NoAccounts);
                return await GoTo<AddAccountState>();
            }
        }

        var choices = new List<string> { Messages.Start.GetActivityInfo, Messages.Start.ManageAccounts, Messages.Common.Exit };
        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(Messages.Common.ChoiceOption)
                .AddChoices(choices));

        return choice switch
        {
            Messages.Start.GetActivityInfo => await GoTo<StartState>(new NamedParameter("configuration", _configuration)),
            Messages.Start.ManageAccounts => await GoTo<ManageAccountsState>(new NamedParameter("configuration", _configuration)),
            _ => new SuccessExecutionResult()
        };
    }
}
