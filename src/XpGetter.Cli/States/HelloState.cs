using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class HelloState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly bool _skipHelloMessage;
    private readonly bool _checkAndPrintAccounts;
    private readonly bool _skipToStart;

    public HelloState(AppConfigurationDto configuration, StateContext context,
                      bool skipHelloMessage = false, bool checkAndPrintAccounts = true,
                      bool skipToStart = false) : base(context)
    {
        _configuration = configuration;
        _skipHelloMessage = skipHelloMessage;
        _checkAndPrintAccounts = checkAndPrintAccounts;
        _skipToStart = skipToStart;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        if (!_skipHelloMessage)
        {
            AnsiConsole.MarkupLine(Messages.Start.Hello);
        }

        if (_checkAndPrintAccounts)
        {
            if (_configuration.Accounts.Any())
            {
                AnsiConsole.MarkupLine(Messages.Start.SavedAccounts,
                    string.Join(", ", _configuration.Accounts.Select(x =>
                        string.Format((string)Messages.Start.SavedAccountFormat, (object?)x.GetDisplayUsername()))));
            }
            else
            {
                AnsiConsole.MarkupLine(Messages.Start.NoAccounts);
                return await GoTo<AddAccountState>();
            }
        }

        if (_skipToStart)
        {
            return await GoToStartState();
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
        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(Messages.Common.ChoiceOption)
                .AddChoices(choices));

        return choice switch
        {
            Messages.Start.GetActivityInfo => await GoToStartState(),
            Messages.Start.Statistics => await GoTo<ChooseStatisticsPeriodState>(new NamedParameter("configuration", _configuration)),
            Messages.Start.ManageAccounts => await GoTo<ManageAccountsState>(new NamedParameter("configuration", _configuration)),
            Messages.Start.CheckForUpdates => await GoTo<CheckUpdatesState>(new NamedParameter("configuration", _configuration)),
            _ => new ExitExecutionResult()
        };

#pragma warning disable CS8974
        ValueTask<StateExecutionResult> GoToStartState()
        {
            return GoTo<StartState>(new NamedParameter("configuration", _configuration),
                                    new NamedParameter("postAuthenticationDelegate", GetActivityInfoDelegate));
        }
#pragma warning restore CS8974
    }

    private async Task<StateExecutionResult> GetActivityInfoDelegate(List<SteamSession> sessions)
    {
        var retrieveActivityStateResult = await AnsiConsole
            .CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                return (RetrieveActivityExecutionResult)await GoTo<RetrieveActivityState>(
                    new NamedParameter("configuration", _configuration),
                    new NamedParameter("sessions", sessions),
                    new NamedParameter("ctx", ctx));
            });

        if (retrieveActivityStateResult.ActivityInfos.Any())
        {
            await GoTo<PrintActivityState>(
                new NamedParameter("configuration", _configuration),
                new NamedParameter("activityInfos", retrieveActivityStateResult.ActivityInfos));
        }

        return retrieveActivityStateResult;
    }
}
