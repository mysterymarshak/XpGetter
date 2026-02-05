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

    public async Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IEnumerable<CsgoItem> items, ECurrencyCode currency)
    {
        var result = await _csgo.GetItemsPriceAsync(items, currency);

        // TODO: try to obtain steam price for each failed item
        if (result.All(x => x.Value == 0))
        {
            _logger.Warning(Messages.Market.FallbackServiceUsedSteam, items.Select(x => x.MarketName));
            result = await _steam.GetItemsPriceAsync(items, currency);
        }

        _logger.Debug(Messages.Market.GotPricesLog, result);
        return result;
    }
}
