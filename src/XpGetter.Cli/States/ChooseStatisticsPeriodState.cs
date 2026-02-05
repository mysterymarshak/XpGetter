using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class ChooseStatisticsPeriodState : BaseState
{
    private readonly AppConfigurationDto _configuration;

    private TimeSpan _timeSpan;

    public ChooseStatisticsPeriodState(AppConfigurationDto configuration, StateContext context) : base(context)
    {
        _configuration = configuration;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var choices = new List<string>
        {
            Messages.Statistics.Days30,
            Messages.Statistics.Days90,
            Messages.Statistics.Days180,
            Messages.Statistics.Days365,
            Messages.Common.Back
        };

        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(Messages.Common.ChoiceOption)
                .AddChoices(choices));

        if (choice == Messages.Common.Back)
        {
            return await GoTo<HelloState>(
                new NamedParameter("configuration", _configuration),
                new NamedParameter("checkAndPrintAccounts", false),
                new NamedParameter("skipHelloMessage", true));
        }

        _timeSpan = choice switch
        {
            Messages.Statistics.Days30 => TimeSpan.FromDays(30),
            Messages.Statistics.Days90 => TimeSpan.FromDays(90),
            Messages.Statistics.Days180 => TimeSpan.FromDays(180),
            Messages.Statistics.Days365 => TimeSpan.FromDays(365),
            _ => throw new ArgumentOutOfRangeException(nameof(choice))
        };

#pragma warning disable CS8974
        return await GoTo<StartState>(new NamedParameter("configuration", _configuration),
                                      new NamedParameter("postAuthenticationDelegate", StatisticsDelegate));
#pragma warning restore CS8974
    }

    private async Task<StateExecutionResult> StatisticsDelegate(List<SteamSession> sessions)
    {
        var retrieveStatisticsStateResult = await AnsiConsole
            .CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                return (StatisticsExecutionResult)await GoTo<RetrieveStatisticsState>(
                    new NamedParameter("configuration", _configuration),
                    new NamedParameter("sessions", sessions),
                    new NamedParameter("timeSpan", _timeSpan),
                    new NamedParameter("ctx", ctx));
            });

        if (retrieveStatisticsStateResult.Statistics.Any())
        {
            return await GoTo<PrintStatisticsState>(
                    new NamedParameter("configuration", _configuration),
                    new NamedParameter("statistics", retrieveStatisticsStateResult.Statistics));
        }

        return retrieveStatisticsStateResult;
    }
}
