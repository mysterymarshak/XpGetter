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

    public async Task<OneOf<Dto.NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropAsync(SteamSession session, IProgressContext ctx)
    {
        var account = session.Account!;
        var getWalletCurrencyTask = ctx.AddTask(account, Messages.Statuses.RetrievingWalletInfo);

        var tasks = new List<Task>
        {
            _walletService.GetWalletInfoAsync(session),
            _newRankDropService.GetLastNewRankDropAsync(session, ctx)
        };

        var getItemsPriceTask = ctx.AddTask(session, Messages.Statuses.RetrievingItemsPriceNotStarted);
        await Task.WhenAll(tasks);

        var getWalletInfoResult =
            ((Task<OneOf<WalletInfo, WalletServiceError>>)tasks[0]).Result;
        var getNewRankDropResult =
            ((Task<OneOf<Dto.NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

        getWalletCurrencyTask.SetResult(session, getWalletInfoResult.IsT0 ?
            Messages.Statuses.RetrievingWalletInfoOk : Messages.Statuses.RetrievingWalletInfoError);

        if (getWalletInfoResult.TryPickT1(out var error, out var walletInfo))
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

        var getItemsPriceResultIsWarning = false;
        var items = newRankDrop.Items!;
        var itemPrices = await _marketService.GetItemsPriceAsync(items, walletInfo.CurrencyCode);

        foreach (var price in itemPrices.Where(x => x.Value == 0))
        {
            // TODO: catch this situations one by one and analyze them, maybe there're just no offers for this item (probably won't happen with weekly drop items) and its not an error
            getItemsPriceResultIsWarning = true;
            _logger.Warning(Messages.Market.InvalidPriceRetrieved, price.MarketName, price.ProviderRaw);
        }

        // TODO: extract item provider to use to config
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

        getItemsPriceTask.SetResult(session, (getItemsPriceResultIsWarning || !itemPrices.Any())
            ? Messages.Statuses.RetrievingItemsPriceError : Messages.Statuses.RetrievingItemsPriceOk);

        return getNewRankDropResult;
    }
}
