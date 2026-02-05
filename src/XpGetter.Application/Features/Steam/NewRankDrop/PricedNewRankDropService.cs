using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Markets;
using XpGetter.Application.Results;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Steam.NewRankDrop;

public class PricedNewRankDropService : INewRankDropService
{
    private readonly INewRankDropService _newRankDropService;
    private readonly IWalletService _walletService;
    private readonly IMarketService _marketService;
    private readonly ILogger _logger;

    public PricedNewRankDropService(INewRankDropService newRankDropService, IWalletService walletService,
        IMarketService marketService, ILogger logger)
    {
        _newRankDropService = newRankDropService;
        _walletService = walletService;
        _marketService = marketService;
        _logger = logger;
    }

    public async Task<OneOf<IReadOnlyList<Dto.NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetNewRankDropsAsync(SteamSession session, DateTimeOffset limit, IProgressContext ctx)
    {
        var getItemsPriceTask = ctx.AddTask(session, Messages.Statuses.RetrievingItemsPriceNotStarted);

        var tasks = new List<Task>
        {
            _walletService.GetWalletInfoAsync(session, ctx),
            _newRankDropService.GetNewRankDropsAsync(session, limit, ctx)
        };

        await Task.WhenAll(tasks);

        var getWalletInfoResult =
            ((Task<OneOf<WalletInfo, WalletServiceError>>)tasks[0]).Result;
        var getNewRankDropsResult =
            ((Task<OneOf<IReadOnlyList<Dto.NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

        if (getWalletInfoResult.TryPickT1(out var walletError, out _))
        {
            _logger.Warning(walletError.Message);
            _logger.Warning(walletError.Exception, string.Empty);

            getItemsPriceTask.SetResult(session, Messages.Statuses.RetrievingItemsPriceError);

            return getNewRankDropsResult;
        }

        if (!getNewRankDropsResult.TryPickT0(out var newRankDrops, out _))
        {
            getItemsPriceTask.SetResult(session, Messages.Statuses.RetrievingItemsPriceError);
            return getNewRankDropsResult;
        }

        getItemsPriceTask.Description(session, Messages.Statuses.RetrievingItemsPrice);

        var items = newRankDrops.SelectMany(x => x.Items!);
        await BindPricesAsync(items, session, getItemsPriceTask);

        return OneOf<IReadOnlyList<Dto.NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>.FromT0(newRankDrops);
    }

    public async Task<OneOf<Dto.NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropAsync(SteamSession session, IProgressContext ctx)
    {
        var account = session.Account!;

        var getItemsPriceTask = ctx.AddTask(session, Messages.Statuses.RetrievingItemsPriceNotStarted);

        var tasks = new List<Task>
        {
            _walletService.GetWalletInfoAsync(session, ctx),
            _newRankDropService.GetLastNewRankDropAsync(session, ctx)
        };

        await Task.WhenAll(tasks);

        var getWalletInfoResult =
            ((Task<OneOf<WalletInfo, WalletServiceError>>)tasks[0]).Result;
        var getNewRankDropResult =
            ((Task<OneOf<Dto.NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

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

        var items = newRankDrop.Items!;
        await BindPricesAsync(items, session, getItemsPriceTask);

        return getNewRankDropResult;
    }

    private async Task BindPricesAsync(IEnumerable<CsgoItem> items, SteamSession session, IProgressTask task)
    {
        var walletInfo = session.WalletInfo;
        var getItemsPriceResultIsWarning = false;
        var itemPrices = await _marketService.GetItemsPriceAsync(items, walletInfo!.CurrencyCode);

        // TODO: also check for distincts between items and itemPrices
        foreach (var price in itemPrices.Where(x => x.Value == 0))
        {
            // TODO: catch this situations one by one and analyze them, maybe there're just no offers for this item (probably won't happen with weekly drop items) and its not an error
            getItemsPriceResultIsWarning = true;
            _logger.Warning(Messages.Market.InvalidPriceRetrieved, price.MarketName, price.ProviderRaw);
        }

        // TODO: extract item provider to use to config (--price-provider)
        foreach (var price in itemPrices.Where(x => x.Provider == PriceProvider.Steam))
        {
            var item = items.FirstOrDefault(x => x.MarketName == price.MarketName);
            if (item is null)
            {
                _logger.Warning(Messages.Market.CannotFindItemForPrice, price.MarketName, items.Select(x => x.MarketName));
                getItemsPriceResultIsWarning = true;
                continue;
            }

            item.BindPrice(price);
        }

        task.SetResult(session, (getItemsPriceResultIsWarning || !itemPrices.Any())
            ? Messages.Statuses.RetrievingItemsPriceError : Messages.Statuses.RetrievingItemsPriceOk);
    }
}
