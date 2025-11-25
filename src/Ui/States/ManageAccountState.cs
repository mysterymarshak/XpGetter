using Autofac;
using Serilog;
using Spectre.Console;
using XpGetter.Configuration;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;
using XpGetter.Steam.Services;

namespace XpGetter.Ui.States;

public class ManageAccountState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly IConfigurationService _configurationService;
    private readonly ISessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger _logger;
    private readonly string _username;

    public ManageAccountState(AppConfigurationDto configuration, string username,
        IConfigurationService configurationService, ISessionService sessionService,
        IAuthenticationService authenticationService, StateContext context, ILogger logger) : base(context)
    {
        _configuration = configuration;
        _configurationService = configurationService;
        _sessionService = sessionService;
        _authenticationService = authenticationService;
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

        var choices = new List<string> { "Remove", "Back", "Exit" };
        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title($"{_username}:")
                .AddChoices(choices));
        var choiceIndex = choices.IndexOf(choice);

        return choiceIndex switch
        {
            0 => await RemoveAccount(account),
            1 => await GoTo<ManageAccountsState>(new NamedParameter("configuration", _configuration)),
            _ => new SuccessExecutionResult()
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
