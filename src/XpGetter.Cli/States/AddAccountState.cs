using Spectre.Console;
using XpGetter.Application;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class AddAccountState : BaseState
{
    private readonly Func<ValueTask<StateExecutionResult>>? _backOption;

    public AddAccountState(StateContext context,
                           Func<ValueTask<StateExecutionResult>>? backOption = null) : base(context)
    {
        _backOption = backOption;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        // Start label to simplify retry logic
        Start:

        var choices = new List<string>(4)
        {
            Messages.AddAccount.ViaPassword,
            Messages.AddAccount.ViaQrCode
        };

        if (_backOption is not null)
        {
            choices.Add(Messages.Common.Back);
        }

        choices.Add(Messages.Common.Exit);

        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(Messages.AddAccount.LogInWay)
                .AddChoices(choices));

        var result = choice switch
        {
            Messages.AddAccount.ViaPassword => await GoTo<AddAccountViaPasswordState>(),
            Messages.AddAccount.ViaQrCode => await GoTo<AddAccountViaQrState>(),
            Messages.Common.Back => await _backOption!.Invoke(),
            _ => new ExitExecutionResult()
        };

        if (result is RetryExecutionResult)
        {
            goto Start;
        }

        return result;
        // end of Start label
    }
}
