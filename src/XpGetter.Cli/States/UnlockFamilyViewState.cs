using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Steam;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class UnlockFamilyViewState : BaseState
{
    private readonly SteamSession _session;
    private readonly IParentalService _parentalService;

    public UnlockFamilyViewState(SteamSession session, IParentalService parentalService,
        StateContext context) : base(context)
    {
        _session = session;
        _parentalService = parentalService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        string? pin = null;
        while (pin is null)
        {
            pin = await AnsiConsole.PromptAsync(
                new TextPrompt<string>(Messages.Parental.EnterThePin)
                    .Secret('*')
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(pin))
            {
                return new UnlockFamilyViewExecutionResult { Success = false };
            }

            if (!ValidatePin(pin))
            {
                pin = null;
                AnsiConsole.MarkupLine(Messages.Parental.InvalidPin);
                continue;
            }

            AnsiConsole.MarkupLine(Messages.Parental.TryingToUnlock);
            var unlockResult = await _parentalService.UnlockFamilyViewAsync(_session, pin);
            if (unlockResult.TryPickT2(out var error, out _))
            {
                return new UnlockFamilyViewExecutionResult
                {
                    Error = new ErrorExecutionResult(() => error.DumpToConsole(Messages.Parental.Error))
                };
            }

            if (unlockResult.IsT1)
            {
                pin = null;
                AnsiConsole.MarkupLine(Messages.Parental.WrongPin);
                continue;
            }

            AnsiConsole.MarkupLine(Messages.Parental.Unlocked);
        }

        return new UnlockFamilyViewExecutionResult { Success = true };
    }

    private bool ValidatePin(string pin) => pin.Length == 4 && pin.All(char.IsDigit);
}
