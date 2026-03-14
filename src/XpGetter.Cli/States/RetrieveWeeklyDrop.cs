using Spectre.Console;
using SteamKit2;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Cs2;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

// TODO: i should rewrite this ugly ass shit
public class RetrieveWeeklyDrop : BaseState
{
    private readonly SteamSession _session;
    private readonly Func<SteamSession, Cs2Client> _resolveCs2Client;

    private bool _forceKickOtherSession;
    private Cs2Client? _cs2Client;

    public RetrieveWeeklyDrop(SteamSession session, Func<SteamSession, Cs2Client> resolveCs2Client,
        StateContext context) : base(context)
    {
        _session = session;
        _resolveCs2Client = resolveCs2Client;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var getAvailableRewardsResult = await AnsiConsole
            .CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);

                if (_cs2Client is null)
                {
                    _cs2Client = _resolveCs2Client(_session);
                    AnsiConsole.MarkupLine(string.Format(Messages.Gc.RetrievingWeeklyDrop, _session.Account!.GetDisplayPersonalNameOrUsername()));
                }

                return await _cs2Client.GetAvailableRewardsAsync(ctx, _forceKickOtherSession);
            });

        if (getAvailableRewardsResult.TryPickT2(out _, out var remainder))
        {
            AnsiConsole.MarkupLine(Messages.Gc.RewardsAlreadyClaimed);
            return new RetrieveWeeklyDropExecutionResult();
        }

        if (remainder.TryPickT1(out _, out var remainder1))
        {
            AnsiConsole.MarkupLine(Messages.Gc.ShutdownSessionDisclaimer1);
            AnsiConsole.MarkupLine(Messages.Gc.ShutdownSessionDisclaimer2);
            AnsiConsole.MarkupLine(Messages.Gc.ShutdownSessionDisclaimer3);
            AnsiConsole.MarkupLine(Messages.Gc.ShutdownSessionDisclaimer4);
            AnsiConsole.MarkupLine(Messages.Gc.ShutdownSessionDisclaimer5);

            using var cts = new CancellationTokenSource();
            var ct = cts.Token;
            var fetcher = _session.MessagesFetcher;
            var disconnectAwaiter = fetcher.CreateAndRegisterAwaiter<SteamClient.DisconnectedCallback>();

            var prompt = new TextPrompt<string>(Messages.Gc.ShutdownSessionPrompt)
                .AddChoice(Messages.Common.Y)
                .AddChoice(Messages.Common.N)
                .DefaultValue(Messages.Common.N);

            var tasks = new List<Task>
            {
                fetcher.WaitForAsync(disconnectAwaiter, ct),
                AwaitClosingCs2(ct),
                AnsiConsole.PromptAsync(prompt, ct)
            };

            var completedTask = await Task.WhenAny(tasks);
            await cts.CancelAsync();

            // i should await PromptAsync task otherwise it would be uncompleted
            // and creating ProgressContext further will throw an exception
            // cancelling (cts.CancelAsync) doesnt help because of implementations details of AnsiConsole
            // so yeah...
            if (completedTask != tasks[2])
            {
                try { await tasks[2]; } catch { }
            }

            if (completedTask == tasks[0])
            {
                return new RetrieveWeeklyDropExecutionResult
                {
                    Error = new ErrorExecutionResult(() => AnsiConsole.MarkupLine(Messages.Session.Disconnected))
                };
            }

            var forceKickSession = false;
            var goBack = false;
            if (completedTask == tasks[1])
            {
                AnsiConsole.MarkupLine(Messages.Gc.OtherClientNoLongerPlayingCs2);
            }
            else if (completedTask == tasks[2])
            {
                var promptResult = ((Task<string>)tasks[2]).Result;
                forceKickSession = promptResult == Messages.Common.Y;
                goBack = promptResult == Messages.Common.N;
            }

            if (goBack)
            {
                AnsiConsole.MarkupLine(Messages.Common.OperationCancelled);
                return new RetrieveWeeklyDropExecutionResult { WaitAnyKey = false };
            }

            _forceKickOtherSession = forceKickSession;
            return await OnExecuted();
        }

        if (remainder1.TryPickT1(out var error, out var availableRewards))
        {
            return new RetrieveWeeklyDropExecutionResult
            {
                Error = new ErrorExecutionResult(() => error.DumpToConsole(Messages.Gc.CannotConnectToGc))
            };
        }

        return new RetrieveWeeklyDropExecutionResult { AvailableItems = availableRewards, Cs2Client = _cs2Client };
    }

    private async Task AwaitClosingCs2(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource();

        Action handler = null!;
        handler = () =>
        {
            if (_session.IsAnotherClientPlayingCs2)
                return;

            _session.PlayingStateChange -= handler;
            tcs.SetResult();
        };

        _session.PlayingStateChange += handler;

        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        try
        {
            await tcs.Task;
        }
        finally
        {
            _session.PlayingStateChange -= handler;
        }
    }
}
