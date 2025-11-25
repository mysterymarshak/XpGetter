using Newtonsoft.Json;

namespace XpGetter.Markets.CsgoMarket;

public class ItemPriceRequest
{
    [JsonProperty("hashNames")]
    public required List<string> ItemNames { get; set; }
}