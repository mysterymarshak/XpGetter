using Newtonsoft.Json;

namespace XpGetter.Application.Features.Markets.CsgoMarket;

public class Price
{
    [JsonProperty("provider")]
    public required string Provider { get; set; }

    [JsonProperty("price")]
    public double Value { get; set; }
}

public class ItemPriceResponse
{
    [JsonProperty("hashName")]
    public required string ItemName { get; set; }

    [JsonProperty("prices")]
    public required List<Price> Prices { get; set; }
}