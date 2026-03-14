using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class ClaimWeeklyDropState : AuthenticatedState
{
    public ClaimWeeklyDropState(StateContext context) : base(context)
    {
    }

    protected override async ValueTask<StateExecutionResult> OnAuthenticated(List<SteamSession> sessions)
    {
        var accountNames = sessions
            .Select(x => x.Account!)
            .Select(x => x.GetDisplayPersonalNameOrUsername());

        var choices = accountNames
            .Append(Messages.Common.Back)
            .ToList();

        var choice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title(Messages.Gc.ChooseAccountToClaimRewards)
                .AddChoices(choices));

        if (choice == Messages.Common.Back)
        {
            AnsiConsole.MarkupLine(Messages.Common.OperationCancelled);
            return SuccessExecutionResult.WithoutWaitingKey();
        }

        var sessionIndex = choices.IndexOf(choice);
        if (sessionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(choice));
        }

        var session = sessions[sessionIndex];

        var retrieveWeeklyDropResult = (RetrieveWeeklyDropExecutionResult)await GoTo<RetrieveWeeklyDrop>(
            new NamedParameter("session", session));

        if (retrieveWeeklyDropResult is { Error: not null } or { AvailableItems: [] })
        {
            return retrieveWeeklyDropResult;
        }

        AnsiConsole.MarkupLine(Messages.Gc.GotAvailableWeeklyDrop);

        var cs2Client = retrieveWeeklyDropResult.Cs2Client!;
        var chooseResult = (ChooseWeeklyDropToClaimExecutionResult)await GoTo<ChooseWeeklyDropToClaim>(
            new NamedParameter("session", session),
            new NamedParameter("retrieveResult", retrieveWeeklyDropResult));

        StateExecutionResult result = chooseResult;

        if (chooseResult.SelectedItems is null)
        {
            AnsiConsole.MarkupLine(Messages.Common.OperationCancelled);
            result = SuccessExecutionResult.WithoutWaitingKey();
        }

        if (chooseResult is { SelectedItems: not null, Error: null })
        {
            var claimResult = await AnsiConsole.CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                return await cs2Client.RedeemRewardsAsync(chooseResult.SelectedItems, ctx);
            });

            if (claimResult.TryPickT1(out var error, out _))
            {
                result = new ClaimWeeklyDropStateExecutionResult
                {
                    Error = new ErrorExecutionResult(() => error.DumpToConsole(Messages.Gc.CannotClaimRewards))
                };
            }
            else
            {
                result = new SuccessExecutionResult();
            }
        }

        cs2Client.Dispose();
        return result;
    }
}
