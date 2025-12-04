using SteamKit2;
using XpGetter.Dto;

namespace XpGetter.Markets;

public interface IMarketService
{
    Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IEnumerable<CsgoItem> items, ECurrencyCode currency);
}