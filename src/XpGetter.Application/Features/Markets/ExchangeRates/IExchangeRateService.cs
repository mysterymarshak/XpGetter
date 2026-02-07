using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Markets.ExchangeRates;

public interface IExchangeRateService
{
    Task<ExchangeRateDto?> GetExchangeRateAsync(ECurrencyCode source, ECurrencyCode target,
                                                SteamSession session, IProgressTask task);
}
