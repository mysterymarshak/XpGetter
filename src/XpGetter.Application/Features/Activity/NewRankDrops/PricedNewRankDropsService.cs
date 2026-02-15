using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Configuration;
using XpGetter.Application.Features.Markets;
using XpGetter.Application.Features.Steam;
using XpGetter.Application.Results;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Activity.NewRankDrops;

public class PricedNewRankDropsService : INewRankDropsService
{
    private readonly INewRankDropsService _newRankDropsService;
    private readonly IWalletService _walletService;
    private readonly IMarketService _marketService;
    private readonly ILogger _logger;

    public PricedNewRankDropsService(INewRankDropsService newRankDropsService, IWalletService walletService,
        IMarketService marketService, ILogger logger)
    {
        _newRankDropsService = newRankDropsService;
        _walletService = walletService;
        _marketService = marketService;
        _logger = logger;
    }

    public async Task<OneOf<IReadOnlyList<NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetNewRankDropsAsync(SteamSession session, DateTimeOffset limit, IProgressContext ctx)
    {
        var getItemsPriceTask = ctx.AddTask(session, Messages.Statuses.RetrievingItemsPriceNotStarted);

        var tasks = new List<Task>
        {
            _walletService.GetWalletInfoAsync(session, ctx),
            _newRankDropsService.GetNewRankDropsAsync(session, limit, ctx)
        };

        await Task.WhenAll(tasks);

        var getWalletInfoResult =
            ((Task<OneOf<WalletInfo, WalletServiceError>>)tasks[0]).Result;
        var getNewRankDropsResult =
            ((Task<OneOf<IReadOnlyList<NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

        if (getWalletInfoResult.TryPickT1(out var walletError, out _))
        {
            _logger.Warning(walletError.Message);
            _logger.Warning(walletError.Exception, string.Empty);

            getItemsPriceTask.SetResult(session, Messages.Statuses.RetrievingItemsPriceError);

            return getNewRankDropsResult;
        }

        IReadOnlyList<NewRankDrop>? newRankDrops = null;

        if (getNewRankDropsResult.TryPickT1(out var tooLongHistory, out _))
        {
            newRankDrops = tooLongHistory.RetrievedNewRankDrops;
            if (newRankDrops.Count == 0)
            {
                return tooLongHistory;
            }
        }
        else if (!getNewRankDropsResult.TryPickT0(out newRankDrops, out _))
        {
            getItemsPriceTask.SetResult(session, Messages.Statuses.RetrievingItemsPriceError);
            return getNewRankDropsResult;
        }

        getItemsPriceTask.Description(session, Messages.Statuses.RetrievingItemsPrice);

        var items = newRankDrops.SelectMany(x => x.Items);
        await BindPricesAsync(items, session, getItemsPriceTask, ctx);

        return OneOf<IReadOnlyList<NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>.FromT0(newRankDrops);
    }

    public async Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropAsync(SteamSession session, IProgressContext ctx)
    {
        var account = session.Account!;

        var getItemsPriceTask = ctx.AddTask(session, Messages.Statuses.RetrievingItemsPriceNotStarted);

        var tasks = new List<Task>
        {
            _walletService.GetWalletInfoAsync(session, ctx),
            _newRankDropsService.GetLastNewRankDropAsync(session, ctx)
        };

        await Task.WhenAll(tasks);

        var getWalletInfoResult =
            ((Task<OneOf<WalletInfo, WalletServiceError>>)tasks[0]).Result;
        var getNewRankDropResult =
            ((Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

        if (getWalletInfoResult.TryPickT1(out var error, out _))
        {
            _logger.Warning(error.Message);
            _logger.Warning(error.Exception, string.Empty);

            getItemsPriceTask.SetResult(session, Messages.Statuses.RetrievingItemsPriceError);

            return getNewRankDropResult;
        }

        if (!getNewRankDropResult.TryPickT0(out var newRankDrop, out _))
        {
            getItemsPriceTask.SetResult(session, Messages.Statuses.RetrievingItemsPriceError);
            return getNewRankDropResult;
        }

        getItemsPriceTask.Description(session, Messages.Statuses.RetrievingItemsPrice);

        var items = newRankDrop.Items;
        await BindPricesAsync(items, session, getItemsPriceTask, ctx);

        return getNewRankDropResult;
    }

    private async Task BindPricesAsync(IEnumerable<CsgoItem> items, SteamSession session,
                                   IProgressTask task, IProgressContext ctx)
    {
        var walletInfo = session.WalletInfo;
        var marketableItems = items
            .Where(x => x.IsMarketable)
            .Select(x => x.MarketName)
            .Distinct()
            .ToList();
        var itemPrices = await _marketService.GetItemsPriceAsync(marketableItems, walletInfo!.CurrencyCode,
                                                                 session, ctx);
        itemPrices = itemPrices
            .Where(x => x.Provider == RuntimeConfiguration.PriceProvider)
            .ToList();

        var getItemsPriceResultIsWarning = itemPrices.Any(x => x.Value == 0) ||
            items.Where(x => x.IsMarketable).Any(x => itemPrices.All(y => y.MarketName != x.MarketName));

        foreach (var price in itemPrices)
        {
            var itemsToBind = items.Where(x => x.MarketName == price.MarketName);
            var anyItemsFound = false;
            foreach (var item in itemsToBind)
            {
                item.BindPrice(price);
                anyItemsFound = true;
            }

            if (!anyItemsFound)
            {
                _logger.Warning(Messages.Market.CannotFindItemForPrice, price.MarketName, items.Select(x => x.MarketName));
                getItemsPriceResultIsWarning = true;
            }
        }

        var taskResult = (getItemsPriceResultIsWarning, itemPrices.Any()) switch
        {
            (_, false) => Messages.Statuses.RetrievingItemsPriceError,
            (true, _) => Messages.Statuses.RetrievingItemsPriceWarning,
            _ => Messages.Statuses.RetrievingItemsPriceOk,
        };
        task.SetResult(session, taskResult);
    }
}
