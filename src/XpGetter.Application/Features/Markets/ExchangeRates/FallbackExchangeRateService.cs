using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.ExchangeRates.ExchangeRateApi;
using XpGetter.Application.Features.ExchangeRates.HexaRateApi;

namespace XpGetter.Application.Features.ExchangeRates;

public class FallbackExchangeRateService : IExchangeRateService
{
    private readonly ExchangeRateApiService _exchangeRateApi;
    private readonly HexaRateApiService _hexaRateApi;
    private readonly ILogger _logger;

    public FallbackExchangeRateService(ExchangeRateApiService exchangeRateApi, HexaRateApiService hexaRateApi,
        ILogger logger)
    {
        _exchangeRateApi = exchangeRateApi;
        _hexaRateApi = hexaRateApi;
        _logger = logger;
    }

    public async Task<ExchangeRateDto?> GetExchangeRateAsync(ECurrencyCode source, ECurrencyCode target)
    {
        var result = await _exchangeRateApi.GetExchangeRateAsync(source, target);

        if (result is null)
        {
            _logger.Warning(Messages.ExchangeRates.FallbackServiceUsedHexa, source, target);

            result = await _hexaRateApi.GetExchangeRateAsync(source, target);

            _logger.Debug(Messages.ExchangeRates.FallbackServiceResult, source, target, result);
        }

        return result;
    }
}
