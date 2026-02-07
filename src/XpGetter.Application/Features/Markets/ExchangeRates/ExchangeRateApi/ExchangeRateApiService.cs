using Newtonsoft.Json;
using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Markets.ExchangeRates.ExchangeRateApi;

public class ExchangeRateApiService : IExchangeRateService
{
    private const string BaseUrl = "https://open.er-api.com/v6/latest/";

    private readonly HttpClient _client;
    private readonly ILogger _logger;

    public ExchangeRateApiService(HttpClient httpClient, ILogger logger)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(BaseUrl);
        _logger = logger;
    }

    public Task<ExchangeRateDto?> GetExchangeRateAsync(ECurrencyCode source, ECurrencyCode target,
                                                       SteamSession session, IProgressTask task)
        => GetExchangeRateInternalAsync(source, target);

    private async Task<ExchangeRateDto?> GetExchangeRateInternalAsync(ECurrencyCode source, ECurrencyCode target)
    {
        try
        {
            var message = new HttpRequestMessage(HttpMethod.Get, source.ToString());
            var result = await _client.SendAsync(message);
            result.EnsureSuccessStatusCode();

            var contentAsString = await result.Content.ReadAsStringAsync();

            var deserialized = JsonConvert.DeserializeObject<ExchangeRateResponse>(contentAsString);
            if (deserialized is null or { Result: not "success" })
            {
                var error = new ExchangeRateServiceError
                {
                    Message = string.Format(Messages.ExchangeRates.DeserializationError, contentAsString)
                };

                _logger.LogError(error);
                return null;
            }

            var rates = deserialized.Rates;
            var rate = (double?)rates!.GetType().GetProperty(target.ToString())?.GetValue(rates);
            if (rate is null)
            {
                var error = new ExchangeRateServiceError
                {
                    Message = string.Format(Messages.ExchangeRates.CurrencyNotFoundError, target, contentAsString)
                };

                _logger.LogError(error);
                return null;
            }

            return new ExchangeRateDto(source, target, rate.Value);
        }
        catch (Exception exception)
        {
            var error = new ExchangeRateServiceError
            {
                Message = string.Format(Messages.ExchangeRates.Exception, source, target),
                Exception = exception
            };

            _logger.LogError(error);
        }

        return null;
    }
}
