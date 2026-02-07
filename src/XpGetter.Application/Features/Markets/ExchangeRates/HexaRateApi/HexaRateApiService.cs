using Newtonsoft.Json;
using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Markets.ExchangeRates.HexaRateApi;

public class HexaRateApiService : IExchangeRateService
{
    private const string BaseUrl = "https://hexarate.paikama.co/api/rates/";

    private readonly HttpClient _client;
    private readonly ILogger _logger;

    public HexaRateApiService(HttpClient httpClient, ILogger logger)
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
            var message = new HttpRequestMessage(HttpMethod.Get, $"{source}/{target}/latest");
            var result = await _client.SendAsync(message);
            result.EnsureSuccessStatusCode();

            var contentAsString = await result.Content.ReadAsStringAsync();

            var deserialized = JsonConvert.DeserializeObject<ExchangeRateResponse>(contentAsString);
            if (deserialized is null or { Status: not 200 })
            {
                var error = new ExchangeRateServiceError
                {
                    Message = string.Format(Messages.ExchangeRates.DeserializationError, contentAsString)
                };

                _logger.LogError(error);
                return null;
            }

            return new ExchangeRateDto(source, target, deserialized.Rate!.Value);
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
