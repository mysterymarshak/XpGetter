using Autofac;
using Spectre.Console;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Ui.States;

public class StartState : BaseState
{
    private readonly AppConfigurationDto _configuration;

    public StartState(AppConfigurationDto configuration, StateContext context) : base(context)
    {
        _configuration = configuration;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        if (!_configuration.Accounts.Any())
        {
            AnsiConsole.MarkupLine(Messages.Start.NoAccounts);
            return await GoTo<AddAccountState>();
        }

        return await GoTo<AuthenticaionState>(new NamedParameter("configuration", _configuration));
    }
}