using Newtonsoft.Json;

namespace XpGetter.Application.Features.Markets.CsgoMarket;

public class ItemPriceRequest
{
    [JsonProperty("hashNames")]
    public required IReadOnlyList<string> ItemNames { get; set; }
}