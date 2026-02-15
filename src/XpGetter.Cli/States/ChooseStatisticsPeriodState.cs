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
    private TimeSpan _timeSpan;

    public ChooseStatisticsPeriodState(StateContext context) : base(context)
    {
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
            return SuccessExecutionResult.WithoutAccountsPrint();
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
        return await GoTo<StartState>(new NamedParameter("postAuthenticationDelegate", StatisticsDelegate));
#pragma warning restore CS8974
    }

    private async Task<StateExecutionResult> StatisticsDelegate(List<SteamSession> sessions)
    {
        var retrieveStatisticsStateResult = await AnsiConsole
            .CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                return (StatisticsExecutionResult)await GoTo<RetrieveStatisticsState>(
                    new NamedParameter("sessions", sessions),
                    new NamedParameter("timeSpan", _timeSpan),
                    new NamedParameter("ctx", ctx));
            });

        if (retrieveStatisticsStateResult.Statistics.Any())
        {
            return await GoTo<PrintStatisticsState>(
                    new NamedParameter("statistics", retrieveStatisticsStateResult.Statistics));
        }

        return retrieveStatisticsStateResult;
    }
}
