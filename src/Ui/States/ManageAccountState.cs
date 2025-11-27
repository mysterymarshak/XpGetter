using Autofac;
using Serilog;
using Spectre.Console;
using XpGetter.Configuration;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Ui.States;

public class ManageAccountState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger _logger;
    private readonly string _username;

    public ManageAccountState(AppConfigurationDto configuration, string username,
        IConfigurationService configurationService, StateContext context, ILogger logger) : base(context)
    {
        _configuration = configuration;
        _configurationService = configurationService;
        _logger = logger;
        _username = username;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var account = _configuration.Accounts.FirstOrDefault(x => x.Username == _username);
        if (account is null)
        {
            AnsiConsole.MarkupLine(Messages.ManageAccounts.AccountWasNotFound, _username);
            return await GoTo<HelloState>(new NamedParameter("configuration", _configuration));
        }

        var choices = new List<string> { Messages.ManageAccounts.Remove, Messages.Common.Back, Messages.Common.Exit };
        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(string.Format(Messages.ManageAccounts.AccountFormat, _username))
                .AddChoices(choices));

        return choice switch
        {
            Messages.ManageAccounts.Remove => await RemoveAccount(account),
            Messages.Common.Back => await GoTo<ManageAccountsState>(new NamedParameter("configuration", _configuration)),
            _ => new ExitExecutionResult()
        };
    }

    private async ValueTask<StateExecutionResult> RemoveAccount(AccountDto account)
    {
        _configuration.RemoveAccount(account.Id);
        _configurationService.WriteConfiguration(_configuration);

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine(Messages.ManageAccounts.AccountRemoved, _username);
        _logger.Debug(Messages.ManageAccounts.AccountRemoved, account.Username);

        return await GoTo<HelloState>(new NamedParameter("configuration", _configuration));
    }
}
