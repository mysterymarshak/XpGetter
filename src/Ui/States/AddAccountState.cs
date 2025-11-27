using Spectre.Console;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Ui.States;

public class AddAccountState : BaseState
{
    private readonly Func<ValueTask<StateExecutionResult>>? _backOption;

    public AddAccountState(StateContext context, Func<ValueTask<StateExecutionResult>>? backOption = null) : base(context)
    {
        _backOption = backOption;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var choices = new List<string>(4) { Messages.AddAccount.ViaPassword, Messages.AddAccount.ViaQrCode };
        if (_backOption is not null)
        {
            choices.Add(Messages.Common.Back);
        }
        choices.Add(Messages.Common.Exit);

        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(Messages.AddAccount.LogInWay)
                .AddChoices(choices));

        return choice switch
        {
            Messages.AddAccount.ViaPassword => await GoTo<AddAccountViaPasswordState>(),
            Messages.AddAccount.ViaQrCode => await GoTo<AddAccountViaQrState>(),
            Messages.Common.Back => await _backOption!.Invoke(),
            _ => new SuccessExecutionResult()
        };
    }
}
