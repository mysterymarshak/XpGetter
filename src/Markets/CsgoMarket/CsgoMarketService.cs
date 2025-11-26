using System.Text;
using Newtonsoft.Json;
using OneOf;
using SteamKit2;
using XpGetter.Dto;
using XpGetter.Errors;

namespace XpGetter.Markets.CsgoMarket;

public class CsgoMarketService : IMarketService
{
    private const string BaseUrl = "https://api.marketapp.online/core/item-price/v2";

    private readonly HttpClient _client;

    public CsgoMarketService(HttpClient httpClient)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<OneOf<IEnumerable<PriceDto>, MarketServiceError>> GetItemsPriceAsync(
        IEnumerable<CsgoItem> items, ECurrencyCode currency)
    {
        try
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"?currency={currency}");
            var payload = new ItemPriceRequest
            {
                ItemNames = items
                    .Where(x => !string.IsNullOrWhiteSpace(x.MarketName))
                    .Select(x => x.MarketName!)
                    .ToList()
            };

            if (payload.ItemNames.Count == 0)
            {
                return OneOf<IEnumerable<PriceDto>, MarketServiceError>.FromT0([]);
            }

            message.Content = new StringContent(JsonConvert.SerializeObject(payload, Formatting.None),
                Encoding.UTF8, "application/json");

            var result = await _client.SendAsync(message);
            result.EnsureSuccessStatusCode();

            var contentAsString = await result.Content.ReadAsStringAsync();

            var deserialized = JsonConvert.DeserializeObject<List<ItemPriceResponse>>(contentAsString);
            if (deserialized is null)
            {
                return new MarketServiceError { Message = $"Cannot deserialize json. Raw: {contentAsString}" };
            }

            return deserialized
                .Select(x => x.Prices.Select(y => new PriceDto(x.ItemName, y.Value, y.Provider)))
                .SelectMany(x => x)
                .ToList();
        }
        catch (Exception exception)
        {
            return new MarketServiceError
            {
                Message = $"An error occured in {nameof(GetItemsPriceAsync)}()",
                Exception = exception
            };
        }
    }
}
