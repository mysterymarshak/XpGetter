using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Markets;

public interface IMarketService
{
    Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IReadOnlyList<string> names, ECurrencyCode currency,
                                                   SteamSession session, IProgressContext ctx);
}
