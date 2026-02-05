using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration;
using XpGetter.Application.Features.Steam;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class UnlockFamilyViewState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly SteamSession _session;
    private readonly IParentalService _parentalService;
    private readonly IConfigurationService _configurationService;

    public UnlockFamilyViewState(AppConfigurationDto configuration, SteamSession session,
        IParentalService parentalService, IConfigurationService configurationService,
            StateContext context) : base(context)
    {
        _configuration = configuration;
        _session = session;
        _parentalService = parentalService;
        _configurationService = configurationService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var account = _session.Account!;
        var savedPin = account.FamilyViewPin;
        string? pin = null;

        while (pin is null)
        {
            if (!string.IsNullOrWhiteSpace(savedPin))
            {
                pin = savedPin;
            }
            else
            {
                pin = await AnsiConsole.PromptAsync(
                    new TextPrompt<string>(Messages.Parental.EnterThePin)
                        .Secret('*')
                        .AllowEmpty());
            }

            if (string.IsNullOrWhiteSpace(pin))
            {
                return new UnlockFamilyViewExecutionResult { Success = false };
            }

            if (!ValidatePin(pin))
            {
                pin = null;

                if (!string.IsNullOrWhiteSpace(savedPin))
                {
                    ResetSavedPin();
                    AnsiConsole.MarkupLine(Messages.Parental.CorruptedSavedPin);

                    continue;
                }

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

                if (!string.IsNullOrWhiteSpace(savedPin))
                {
                    ResetSavedPin();
                    AnsiConsole.MarkupLine(Messages.Parental.WrongSavedPin);
                }
                else
                {
                    AnsiConsole.MarkupLine(Messages.Parental.WrongPin);
                }

                continue;
            }

            AnsiConsole.MarkupLine(Messages.Parental.Unlocked);

            if (pin != savedPin)
            {
                var savePinPrompt = new TextPrompt<string>(Messages.Parental.SavePinPrompt)
                    .AddChoice(Messages.Common.Y)
                    .AddChoice(Messages.Common.N)
                    .DefaultValue(Messages.Common.Y);

                var savePinPromptResult = await AnsiConsole.PromptAsync(savePinPrompt);
                if (savePinPromptResult == Messages.Common.Y)
                {
                    account.FamilyViewPin = pin;
                    _configurationService.WriteConfiguration(_configuration);

                    AnsiConsole.MarkupLine(Messages.Parental.PinSaved);
                }
            }
        }

        return new UnlockFamilyViewExecutionResult { Success = true };

        void ResetSavedPin()
        {
            savedPin = null;
            account.FamilyViewPin = null;
            _configurationService.WriteConfiguration(_configuration);
        }
    }

    private bool ValidatePin(string pin) => pin.Length == 4 && pin.All(char.IsDigit);
}
