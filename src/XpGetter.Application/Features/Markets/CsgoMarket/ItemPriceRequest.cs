using Newtonsoft.Json;

namespace XpGetter.Application.Features.Markets.CsgoMarket;

public class ItemPriceRequest
{
    [JsonProperty("hashNames")]
    public required List<string> ItemNames { get; set; }
}