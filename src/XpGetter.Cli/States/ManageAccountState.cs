using Serilog;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class ManageAccountState : BaseState
{
    private readonly string _username;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger _logger;

    public ManageAccountState(string username, IConfigurationService configurationService,
                              StateContext context, ILogger logger) : base(context)
    {
        _username = username;
        _configurationService = configurationService;
        _logger = logger;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var account = Configuration.Accounts
            .FirstOrDefault(x => x.Username == _username);

        if (account is null)
        {
            AnsiConsole.MarkupLine(Messages.ManageAccounts.AccountWasNotFound, _username);
            return new SuccessExecutionResult();
        }

        var choices = new List<string>
        {
            Messages.ManageAccounts.Remove,
            Messages.Common.Back,
            Messages.Common.Exit
        };

        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(string.Format(Messages.ManageAccounts.AccountFormat, account.GetDisplayUsername()))
                .AddChoices(choices));

        return choice switch
        {
            Messages.ManageAccounts.Remove => await RemoveAccount(account),
            Messages.Common.Back => await GoTo<ManageAccountsState>(),
            _ => new ExitExecutionResult()
        };
    }

    private async ValueTask<StateExecutionResult> RemoveAccount(AccountDto account)
    {
        Configuration.RemoveAccount(account.Id);
        _configurationService.WriteConfiguration(Configuration);

        AnsiConsole.MarkupLine(Messages.ManageAccounts.AccountRemoved, account.GetDisplayUsername());
        _logger.Debug(Messages.ManageAccounts.AccountRemoved, account.Username);

        return new SuccessExecutionResult();
    }
}
