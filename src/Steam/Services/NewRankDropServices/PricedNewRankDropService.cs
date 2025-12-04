using OneOf;
using Serilog;
using XpGetter.Dto;
using XpGetter.Errors;
using XpGetter.Markets;
using XpGetter.Results;
using XpGetter.Utils.Progress;

namespace XpGetter.Steam.Services.NewRankDropServices;

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

    public async Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
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
            ((Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

        getWalletCurrencyTask.SetResult(session, getNewRankDropResult.IsT0 ?
            Messages.Statuses.RetrievingWalletInfoOk : Messages.Statuses.RetrievingWalletInfoError);

        if (getWalletInfoResult.TryPickT1(out var error, out var walletInfo))
        {
            _logger.Warning(error.Message);
            _logger.Warning(error.Exception, string.Empty);

            return getNewRankDropResult;
        }

        if (!getNewRankDropResult.TryPickT0(out var newRankDrop, out _))
        {
            return getNewRankDropResult;
        }

        getItemsPriceTask.Description(session, Messages.Statuses.RetrievingItemsPrice);
        var getItemsPriceResultIsWarning = false;
        var items = newRankDrop.Items;
        var itemPrices = await _marketService.GetItemsPriceAsync(items, walletInfo.CurrencyCode);
        foreach (var price in itemPrices)
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
            ? Messages.Statuses.RetrievingItemsPriceWarning : Messages.Statuses.RetrievingItemsPriceOk);

        return getNewRankDropResult;
    }
}
