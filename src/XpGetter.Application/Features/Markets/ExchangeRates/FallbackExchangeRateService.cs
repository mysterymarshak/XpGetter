using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Markets.ExchangeRates.ExchangeRateApi;
using XpGetter.Application.Features.Markets.ExchangeRates.HexaRateApi;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Markets.ExchangeRates;

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

    public async Task<ExchangeRateDto?> GetExchangeRateAsync(ECurrencyCode source, ECurrencyCode target,
                                                             SteamSession session, IProgressTask task)
    {
        var result = await _exchangeRateApi.GetExchangeRateAsync(source, target, session, task);

        if (result is null)
        {
            task.Description(session, Messages.Statuses.RetrievingExchangeRateFallback);
            _logger.Warning(Messages.ExchangeRates.FallbackServiceUsedHexa, source, target);

            result = await _hexaRateApi.GetExchangeRateAsync(source, target, session, task);

            _logger.Debug(Messages.ExchangeRates.FallbackServiceResult, source, target, result);
        }

        return result;
    }
}
