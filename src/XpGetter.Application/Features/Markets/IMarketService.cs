using SteamKit2;
using XpGetter.Application.Dto;

namespace XpGetter.Application.Features.Markets;

public interface IMarketService
{
    Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IReadOnlyList<string> names, ECurrencyCode currency);
}
