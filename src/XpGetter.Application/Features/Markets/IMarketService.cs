using SteamKit2;
using XpGetter.Application.Dto;

namespace XpGetter.Application.Features.Markets;

public interface IMarketService
{
    Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IEnumerable<string> names, ECurrencyCode currency);
}
