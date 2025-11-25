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
        const string passwordChoice = "Username/password";
        const string qrChoice = "QR-code";
        const string backChoice = "Back";
        const string exitChoice = "Exit";

        var choices = new List<string>(4) { passwordChoice, qrChoice };
        if (_backOption is not null)
        {
            choices.Add(backChoice);
        }
        choices.Add(exitChoice);

        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Choice auth way:")
                .AddChoices(choices));

        return choice switch
        {
            passwordChoice => await GoTo<AddAccountViaPasswordState>(),
            qrChoice => await GoTo<AddAccountViaQrState>(),
            backChoice => await _backOption!.Invoke(),
            _ => new SuccessExecutionResult()
        };
    }
}
