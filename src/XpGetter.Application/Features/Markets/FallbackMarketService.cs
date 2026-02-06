using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Markets.CsgoMarket;
using XpGetter.Application.Features.Markets.SteamMarket;

namespace XpGetter.Application.Features.Markets;

public class FallbackMarketService : IMarketService
{
    private readonly CsgoMarketService _csgo;
    private readonly SteamMarketService _steam;
    private readonly ILogger _logger;

    public FallbackMarketService(CsgoMarketService csgo, SteamMarketService steam, ILogger logger)
    {
        _csgo = csgo;
        _steam = steam;
        _logger = logger;
    }

    public async Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IEnumerable<string> names, ECurrencyCode currency)
    {
        var result = await _csgo.GetItemsPriceAsync(names, currency);

        var failedItems = result
            .Where(x => x.Value == 0)
            .Select(x => x.MarketName)
            .Concat(names.Where(x => result.All(y => y.MarketName != x)))
            .ToList();

        if (failedItems.Count > 0)
        {
            _logger.Warning(Messages.Market.FallbackServiceUsedSteam, failedItems);

            var failedItemsResult = await _steam.GetItemsPriceAsync(failedItems, currency);
            result = result.Concat(failedItemsResult);

            _logger.Debug(Messages.Market.FallbackServiceResult, failedItemsResult);
        }

        _logger.Debug(Messages.Market.GotPricesLog, result);
        return result;
    }
}
