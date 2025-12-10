using System.Text;
using Newtonsoft.Json;
using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;

namespace XpGetter.Application.Features.Markets.CsgoMarket;

public class CsgoMarketService : IMarketService
{
    private const string BaseUrl = "https://api.marketapp.online/core/item-price/v2";

    private readonly HttpClient _client;
    private readonly ILogger _logger;

    public CsgoMarketService(HttpClient httpClient, ILogger logger)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(BaseUrl);
        _logger = logger;
    }

    public async Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IEnumerable<CsgoItem> items, ECurrencyCode currency)
    {
        var names = items
            .Where(x => !string.IsNullOrWhiteSpace(x.MarketName))
            .Select(x => x.MarketName!)
            .ToList();

        if (names.Count == 0)
        {
            return [];
        }

        try
        {
            var payload = new ItemPriceRequest { ItemNames = names };
            var message = new HttpRequestMessage(HttpMethod.Post, $"?currency={currency}");
            message.Content = new StringContent(JsonConvert.SerializeObject(payload, Formatting.None),
                Encoding.UTF8, "application/json");

            var result = await _client.SendAsync(message);
            result.EnsureSuccessStatusCode();

            var contentAsString = await result.Content.ReadAsStringAsync();

            var deserialized = JsonConvert.DeserializeObject<List<ItemPriceResponse>>(contentAsString);
            if (deserialized is null)
            {
                var error = new MarketServiceError
                {
                    Message = string.Format(Messages.Market.DeserializationError, contentAsString)
                };

                _logger.LogError(error);
                return [];
            }

            return deserialized
                .Select(x => x.Prices.Select(y =>
                    new PriceDto(x.ItemName, y.Value, y.Provider, currency)))
                .SelectMany(x => x)
                .ToList();
        }
        catch (Exception exception)
        {
            var error = new MarketServiceError
            {
                Message = string.Format(Messages.Market.GetPriceException, string.Join(", ", names)),
                Exception = exception
            };

            _logger.LogError(error);
        }

        return [];
    }
}