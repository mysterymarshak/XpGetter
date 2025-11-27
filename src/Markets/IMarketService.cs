using OneOf;
using SteamKit2;
using XpGetter.Dto;
using XpGetter.Errors;

namespace XpGetter.Markets;

public interface IMarketService
{
    Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IEnumerable<CsgoItem> items, ECurrencyCode currency);
}