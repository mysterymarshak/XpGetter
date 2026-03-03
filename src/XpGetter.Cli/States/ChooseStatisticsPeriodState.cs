using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Cli.Extensions;
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

        return await GoTo<RetrieveStatisticsState>(new NamedParameter("timeSpan", _timeSpan));
    }
}
