using OneOf;
using SteamKit2;
using XpGetter.Dto;
using XpGetter.Errors;
using XpGetter.Extensions;
using XpGetter.Steam.Http.Clients;

namespace XpGetter.Markets.SteamMarket;

public class SteamMarketService : IMarketService
{
    private const string Uri = "market/priceoverview";

    private readonly ISteamHttpClient _steamHttpClient;

    public SteamMarketService(ISteamHttpClient steamHttpClient)
    {
        _steamHttpClient = steamHttpClient;
    }

    public async Task<OneOf<IEnumerable<PriceDto>, MarketServiceError>> GetItemsPriceAsync(
        IEnumerable<CsgoItem> items, ECurrencyCode currency)
    {
        var names = items
            .Where(x => !string.IsNullOrWhiteSpace(x.MarketName))
            .Select(x => x.MarketName!)
            .ToList();

        if (names.Count == 0)
        {
            return OneOf<IEnumerable<PriceDto>, MarketServiceError>.FromT0([]);
        }

        var getItemPriceTasks = names.Select(x => GetItemPriceAsync(x, currency));
        var getItemPriceResults = await Task.WhenAll(getItemPriceTasks);

        foreach (var (i, itemPriceResult) in getItemPriceResults.Index())
        {
            if (itemPriceResult.TryPickT1(out var error, out _))
            {
                var itemName = names[i];
                error.DumpToConsole("An error occurred while retrieving item price for '{0}'", itemName);
            }
        }

        var successResults = getItemPriceResults
            .Select((x, i) => new { Result = x, ItemName = names[i] })
            .Where(x => x.Result.IsT0)
            .Select(x => (x.Result.AsT0.Deserialized, x.ItemName))
            .Where(x => x.Deserialized.Success)
            .ToList();

        return successResults
            .Select(result => new PriceDto(result.ItemName, result.Deserialized.Value, "Steam"))
            .ToList();
    }

    private async Task<OneOf<(ItemPriceResponse Deserialized, string Raw), SteamHttpClientError>> GetItemPriceAsync(
        string name, ECurrencyCode currency)
    {
        var queryString = $"currency={(int)currency}&appid=730&market_hash_name={name}";
        return await _steamHttpClient.GetJsonAsync<ItemPriceResponse>($"{Uri}?{queryString}");
    }
}