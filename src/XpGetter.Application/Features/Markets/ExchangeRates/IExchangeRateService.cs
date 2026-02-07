using SteamKit2;
using XpGetter.Application.Dto;

namespace XpGetter.Application.Features.ExchangeRates;

public interface IExchangeRateService
{
    Task<ExchangeRateDto?> GetExchangeRateAsync(ECurrencyCode source, ECurrencyCode target);
}
