using System.Web;
using OneOf;
using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Steam.Http;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Markets.SteamMarket;

public class SteamMarketService : IMarketService
{
    private const string Uri = "market/priceoverview";

    private readonly ISteamHttpClient _steamHttpClient;
    private readonly ILogger _logger;

    public SteamMarketService(ISteamHttpClient steamHttpClient, ILogger logger)
    {
        _steamHttpClient = steamHttpClient;
        _logger = logger;
    }

    public Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IReadOnlyList<string> names,
        ECurrencyCode currency, SteamSession session, IProgressContext ctx)
        => GetItemsPriceInternalAsync(names, currency);

    private async Task<IEnumerable<PriceDto>> GetItemsPriceInternalAsync(IReadOnlyList<string> names,
                                                                         ECurrencyCode currency)
    {
        if (names.Count == 0)
        {
            return [];
        }

        var getItemPriceTasks = names.Select(x => GetItemPriceAsync(x, currency));
        var getItemPriceResults = await Task.WhenAll(getItemPriceTasks);

        foreach (var (i, itemPriceResult) in getItemPriceResults.Index())
        {
            if (itemPriceResult.TryPickT1(out var error, out _))
            {
                var itemName = names[i];
                _logger.Error(Messages.Market.GetPriceException, itemName);
                _logger.LogError(error);
            }
        }

        var successResults = getItemPriceResults
            .Select((x, i) => new { Result = x, ItemName = names[i] })
            .Where(x => x.Result.IsT0)
            .Select(x => (x.Result.AsT0.Deserialized, x.ItemName))
            .Where(x => x.Deserialized.Success)
            .ToList();

        if (successResults.Count == 0)
        {
            return [];
        }

        return successResults
            .Select(result =>
                new PriceDto(result.ItemName, result.Deserialized.Value, "Steam", currency))
            .ToList();
    }

    private async Task<OneOf<(ItemPriceResponse Deserialized, string Raw), SteamHttpClientError>> GetItemPriceAsync(
        string name, ECurrencyCode currency)
    {
        var queryString = $"currency={(int)currency}&appid=730&market_hash_name={HttpUtility.UrlEncode(name)}";
        return await _steamHttpClient.GetJsonAsync<ItemPriceResponse>($"{Uri}?{queryString}");
    }
}
