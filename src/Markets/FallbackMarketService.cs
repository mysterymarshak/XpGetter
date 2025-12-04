using SteamKit2;
using XpGetter.Dto;
using XpGetter.Markets.CsgoMarket;
using XpGetter.Markets.SteamMarket;

namespace XpGetter.Markets;

public class FallbackMarketService : IMarketService
{
    private readonly CsgoMarketService _csgo;
    private readonly SteamMarketService _steam;

    public FallbackMarketService(CsgoMarketService csgo, SteamMarketService steam)
    {
        _csgo = csgo;
        _steam = steam;
    }

    public async Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IEnumerable<CsgoItem> items, ECurrencyCode currency)
    {
        var result = await _csgo.GetItemsPriceAsync(items, currency);

        if (!result.Any())
        {
            result = await _steam.GetItemsPriceAsync(items, currency);
        }

        return result;
    }
}
