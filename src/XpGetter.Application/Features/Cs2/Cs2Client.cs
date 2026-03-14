using OneOf;
using OneOf.Types;
using ProtoBuf;
using Serilog;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.CSGO.Internal;
using SteamKit2.Internal;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Cs2.Enums;
using XpGetter.Application.Features.Io;
using XpGetter.Application.Results;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Cs2;

public class Cs2Client
{
    private const int RewardsCount = 2;

    private static KvTree? _itemsGame;
    private static KvTree? _english;

    private readonly SteamSession _session;
    private readonly IKvsProvider _kvsProvider;
    private readonly IFilesAccessor _filesAccessor;
    private readonly ILogger _logger;

    private CMsgClientWelcome? _cachedWelcome;
    private CSOAccountItemPersonalStore? _cachedStore;

    public Cs2Client(SteamSession session, IKvsProvider kvsProvider, IFilesAccessor filesAccessor, ILogger logger)
    {
        _session = session;
        _kvsProvider = kvsProvider;
        _filesAccessor = filesAccessor;
        _logger = logger;
    }

    public async Task<OneOf<List<Cs2InventoryItem>, AlreadyPlayingCs2, RewardsAlreadyClaimed, Cs2ClientError>>
        GetAvailableRewardsAsync(IProgressContext ctx, bool forceKickOtherSession = false)
    {
        var tasks = new List<Task>
        {
            ConnectToGcAsync(ctx, forceKickOtherSession),
            GetKvAsync(KvFileType.ItemsGame, ctx),
            GetKvAsync(KvFileType.Localization, ctx)
        };
        await Task.WhenAll(tasks);

        var connectToGcResult =
            ((Task<OneOf<CMsgClientWelcome, AlreadyPlayingCs2, Cs2ClientError>>)tasks[0]).Result;
        var getItemsGameResult =
            ((Task<OneOf<KvTree, Cs2ClientError>>)tasks[1]).Result;
        var getLocalizationResult =
            ((Task<OneOf<KvTree, Cs2ClientError>>)tasks[2]).Result;

        if (connectToGcResult.TryPickT1(out var alreadyPlayingCs2, out var remainder))
        {
            return alreadyPlayingCs2;
        }

        if (remainder.TryPickT1(out var error, out var clientWelcome))
        {
            return error;
        }

        if (getItemsGameResult.TryPickT1(out var getKvError, out var itemGameTree)
            || getLocalizationResult.TryPickT1(out getKvError, out var englishTree))
        {
            return getKvError;
        }

        _itemsGame = itemGameTree;
        _english = englishTree;

        // TODO: parse only weekly drop items
        var inventoryParser = new Cs2InventoryParser(clientWelcome, _itemsGame, _english, _logger);
        var inventory = inventoryParser.Parse();

        var getAvailableRewardsResult = GetAvailableRewardsList(inventory, clientWelcome);
        if (getAvailableRewardsResult.TryPickT1(out var alreadyClaimed, out var remainder1))
        {
            return alreadyClaimed;
        }

        if (remainder1.TryPickT1(out error, out var availableRewards))
        {
            return error;
        }

        return OneOf<List<Cs2InventoryItem>, AlreadyPlayingCs2, RewardsAlreadyClaimed, Cs2ClientError>.FromT0(availableRewards);
    }

    public async Task<OneOf<Success, Cs2ClientError>> RedeemRewardsAsync(
        List<Cs2InventoryItem> items, IProgressContext ctx)
    {
        if (FailIf(items.Count != RewardsCount, Messages.Gc.RedeemItemsCount, out Cs2ClientError? error))
        {
            return error;
        }

        if (!_session.IsPlayingCs2)
        {
            return new Cs2ClientError { Message = Messages.Gc.DisconnectedFromGc };
        }

        var task = ctx.AddTask(Messages.Statuses.RedeemingRewards);

        try
        {
            var fetcher = _session.MessagesFetcher;
            var gcFetcher = fetcher.Gc;
            var gameCoordinator = _session.Client.GetHandler<SteamGameCoordinator>()!;

            // TODO: listeners instead of awaiters and catch CMsgGCItemCustomizationNotification
            // with request = 9219 (EGCItemCustomizationNotification.k_EGCItemCustomizationNotification_ClientRedeemFreeReward)
            var notificationAwaiter = gcFetcher.CreateAndRegisterAwaiter<CMsgGCItemCustomizationNotification>(
                (uint)EGCItemMsg.k_EMsgGCItemCustomizationNotification);

            RedeemPlayerFreeRewards(gameCoordinator, items[0].EconItem.id, items[1].EconItem.id);

            // GC messages sequence
            // ESOMsg.k_ESOMsg_Destroy CSOEconItem
            // ESOMsg.k_ESOMsg_Destroy CSOEconItem
            // ESOMsg.k_ESOMsg_Create CSOEconItem
            // ESOMsg.k_ESOMsg_Create CSOEconItem
            // ESOMsg.k_ESOMsg_UpdateMultiple CMsgSOMultipleObjects:
            // type_id 2 CSOPersonaDataPublic
            // type_id 4 CMsgGCItemCustomizationNotification
            // EGCItemMsg.k_EMsgGCItemCustomizationNotification CMsgGCItemCustomizationNotification <- I catch this
            // request will equal EGCItemCustomizationNotification.k_EGCItemCustomizationNotification_ClientRedeemFreeReward
            // and item_id will contain new inventory ids

            await gcFetcher.WaitForAsync(notificationAwaiter);
            task.SetResult(Messages.Statuses.RedeemRewardsOk);

            return new Success();
        }
        catch (TaskCanceledException)
        {
            error = new Cs2ClientError
            {
                Message = Messages.Common.TimeoutException
            };
        }
        catch (Exception exception)
        {
            error = new Cs2ClientError
            {
                Message = Messages.Gc.ConnectionException,
                Exception = exception
            };
        }

        task.SetResult(Messages.Statuses.RedeemRewardsError);
        return error;
    }

    public void Dispose()
    {
        // fire and forget
        PlayGame(0);
    }

    private async Task<OneOf<KvTree, Cs2ClientError>> GetKvAsync(KvFileType type, IProgressContext ctx)
    {
        if (type == KvFileType.ItemsGame && _itemsGame is not null)
        {
            ctx.AddFinishedTask(string.Format(Messages.Statuses.DownloadingOk, type));
            return _itemsGame;
        }

        if (type == KvFileType.Localization && _english is not null)
        {
            ctx.AddFinishedTask(string.Format(Messages.Statuses.DownloadingOk, type));
            return _english;
        }

        var getFileResult = await _kvsProvider.GetFileAsync(type, ctx);
        if (getFileResult.TryPickT2(out var error, out var remainder))
        {
            return new Cs2ClientError
            {
                Message = error.Message,
                Exception = error.Exception
            };
        }

        var parsingTask = ctx.AddTask(string.Format(Messages.Statuses.ParsingKvs, type));

        try
        {
            var tree = remainder.Match(
                filePath => KvTree.ReadFromFile(type, filePath, _filesAccessor),
                bytes => KvTree.ReadFromBytes(type, bytes, _filesAccessor));

            parsingTask.SetResult(string.Format(Messages.Statuses.ParsingKvsOk, type));
            return tree;
        }
        catch (Exception exception)
        {
            parsingTask.SetResult(string.Format(Messages.Statuses.ParsingKvsError, type));
            return new Cs2ClientError
            {
                Message = Messages.Gc.ParsingKvsError,
                Exception = exception
            };
        }
    }

    private async Task<OneOf<CMsgClientWelcome, AlreadyPlayingCs2, Cs2ClientError>>
        ConnectToGcAsync(IProgressContext ctx, bool forceKickOtherSession = false)
    {
        var task = ctx.AddTask(Messages.Statuses.ConnectingToGc);

        if (_session.IsPlayingCs2)
        {
            task.SetResult(Messages.Statuses.ConnectingToGcOk);
            return _cachedWelcome!;
        }

        var playCs2Result = await TryPlayCs2(forceKickOtherSession);
        if (!playCs2Result.TryPickT0(out _, out var remainder))
        {
            task.SetResult(Messages.Statuses.ConnectingToGcError);
            return remainder.Match<OneOf<CMsgClientWelcome, AlreadyPlayingCs2, Cs2ClientError>>(
                alreadyPlayingCs2 => alreadyPlayingCs2,
                error => error);
        }

        _logger.Debug(Messages.Gc.PlayingCs2.BindSession(_session));

        Cs2ClientError? error;

        try
        {
            var fetcher = _session.MessagesFetcher;
            var gcFetcher = fetcher.Gc;
            var gameCoordinator = _session.Client.GetHandler<SteamGameCoordinator>()!;
            var clientWelcomeAwaiter = gcFetcher.CreateAndRegisterAwaiter<CMsgClientWelcome>((uint)EGCBaseClientMsg.k_EMsgGCClientWelcome);

            SendCs2Hello(gameCoordinator);
            var clientWelcomeCallback = await gcFetcher.WaitForAsync(clientWelcomeAwaiter);

            _logger.Debug(Messages.Gc.ConnectedToGc.BindSession(_session));
            task.SetResult(Messages.Statuses.ConnectingToGcOk);

            _cachedWelcome = clientWelcomeCallback.Body;
            // Serializer.Deserialize<CMsgCStrike15Welcome>(clientWelcomeCallback.Body.game_data)
            // Serializer.Deserialize<CMsgGCCStrike15_v2_MatchmakingGC2ClientHello>(clientWelcomeCallback.Body.game_data2)
            return _cachedWelcome;
        }
        catch (TaskCanceledException)
        {
            error = new Cs2ClientError
            {
                Message = Messages.Common.TimeoutException
            };
        }
        catch (Exception exception)
        {
            error = new Cs2ClientError
            {
                Message = Messages.Gc.ConnectionException,
                Exception = exception
            };
        }

        task.SetResult(Messages.Statuses.ConnectingToGcError);
        return error;
    }

    private async Task<OneOf<Success, AlreadyPlayingCs2, Cs2ClientError>> TryPlayCs2(bool forceKickOtherSession)
    {
        if (_session.IsAnotherClientPlayingCs2)
        {
            _logger.Information(Messages.Gc.AnotherClientPlayingCs2.BindSession(_session));

            if (!forceKickOtherSession)
            {
                return new AlreadyPlayingCs2();
            }

            const int retriesCount = 3;
            var retryNumber = 1;
            var success = false;

            while (!success && retryNumber <= retriesCount)
            {
                success = await KickOtherPlayingSessionAsync();
                retryNumber++;
            }

            if (!success)
            {
                return new AlreadyPlayingCs2();
            }
        }

        try
        {
            var fetcher = _session.MessagesFetcher;
            var playingStateAwaiter = fetcher.CreateAndRegisterAwaiter<SteamUser.PlayingSessionStateCallback>();
            var logOffAwaiter = fetcher.CreateAndRegisterAwaiter<SteamUser.LoggedOffCallback>();

            PlayGame(Constants.Cs2AppId);
            var playGameResult = await fetcher.WaitForAnyAsync(playingStateAwaiter, logOffAwaiter);

            if (playGameResult.IsT1)
            {
                return new AlreadyPlayingCs2();
            }

            if (!Assert(_session.IsPlayingCs2, Messages.Gc.NotPlayingCs2, _session, out Cs2ClientError? error))
            {
                return error;
            }

            return new Success();
        }
        catch (TaskCanceledException)
        {
            return new Cs2ClientError
            {
                Message = Messages.Common.TimeoutException
            };
        }
        catch (Exception exception)
        {
            return new Cs2ClientError
            {
                Message = Messages.Gc.ConnectionException,
                Exception = exception
            };
        }
    }

    private OneOf<List<Cs2InventoryItem>, RewardsAlreadyClaimed, Cs2ClientError> GetAvailableRewardsList(
        Dictionary<ulong, Cs2InventoryItem> inventory, CMsgClientWelcome clientWelcome)
    {
        var freeRewards = clientWelcome
            .outofdate_subscribed_caches
            .FirstOrDefault()?
            .objects
            .FirstOrDefault(x => x.type_id == (uint)SoCacheType.WeeklyDrop);

        if (FailIfNull(freeRewards, Messages.Gc.FreeRewardsNotFound, _session, out Cs2ClientError? error))
        {
            return error;
        }

        var storeObject = freeRewards.object_data.Single();
        _cachedStore = Serializer.Deserialize<CSOAccountItemPersonalStore>(storeObject);
        if (_cachedStore.redeemable_balance < RewardsCount)
        {
            return new RewardsAlreadyClaimed();
        }

        var availableItems = _cachedStore.items
            .Select(x => inventory.First(y => x == y.Key).Value)
            .ToList();

        // TODO: print available items
        if (FailIf(availableItems.Count != 4, Messages.Gc.FreeRewardsCountMismatch, _session, out error))
        {
            return error;
        }

        return OneOf<List<Cs2InventoryItem>, RewardsAlreadyClaimed, Cs2ClientError>.FromT0(availableItems);
    }

    // private void RequestPlayerProfile(uint accountId, SteamGameCoordinator gameCoordinator)
    // {
    //     var msg = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_ClientRequestPlayersProfile>(
    //         (uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_ClientRequestPlayersProfile)
    //     {
    //         Body =
    //         {
    //             account_id = accountId,
    //             request_level = 32
    //         }
    //     };
    //
    //     gameCoordinator.Send(msg, Constants.Cs2AppId);
    // }

    private void RedeemPlayerFreeRewards(SteamGameCoordinator gameCoordinator, ulong id1, ulong id2)
    {
        var msg = new ClientGCMsgProtobuf<CMsgGCCstrike15_v2_ClientRedeemFreeReward>(
            (uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_ClientRedeemFreeReward);

        ThrowIfNull(_cachedStore, Messages.Common.ShouldNotBeNull);

        msg.Body.generation_time = _cachedStore.generation_time;
        msg.Body.redeemable_balance = RewardsCount;
        msg.Body.items.Add(id1);
        msg.Body.items.Add(id2);

        gameCoordinator.Send(msg, Constants.Cs2AppId);
    }

    private async Task<bool> KickOtherPlayingSessionAsync()
    {
        var kickMsg = new ClientMsgProtobuf<CMsgClientKickPlayingSession>(EMsg.ClientKickPlayingSession)
        {
            Body =
            {
                only_stop_game = true
            }
        };

        var client = _session.Client;
        var fetcher = _session.MessagesFetcher;
        var playingStateAwaiter = fetcher.CreateAndRegisterAwaiter<SteamUser.PlayingSessionStateCallback>();

        try
        {
            client.Send(kickMsg);

            var playingStateCallback = await fetcher.WaitForAsync(playingStateAwaiter);
            var success = playingStateCallback.PlayingAppID == 0;
            if (!success)
            {
                _logger.Warning(Messages.Gc.CannotShutDownPlayingSession.BindSession(_session), playingStateCallback);
            }

            return success;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private void PlayGame(int appId)
    {
        var gamesPlayedRequest = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayedWithDataBlob)
        {
            Body =
            {
                client_os_type = unchecked((uint)EOSType.Unknown),
                games_played = { new CMsgClientGamesPlayed.GamePlayed { game_id = new GameID(appId) } }
            }
        };

        _session.Client.Send(gamesPlayedRequest);
    }

    private void SendCs2Hello(SteamGameCoordinator gameCoordinator)
    {
        var msg = new ClientGCMsgProtobuf<SteamKit2.GC.CSGO.Internal.CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello)
        {
            Body =
            {
                version = 2000600,
                client_session_need = 0,
                client_launcher = 0,
                steam_launcher = 0
            }
        };

        gameCoordinator.Send(msg, Constants.Cs2AppId);
    }
}
