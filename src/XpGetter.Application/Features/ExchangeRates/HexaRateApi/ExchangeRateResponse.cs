using Newtonsoft.Json;

namespace XpGetter.Application.Features.ExchangeRates.HexaRateApi;

public class Rate
{
    [JsonProperty("base")]
    public required string SourceCurrency { get; set; }

    [JsonProperty("target")]
    public required string TargetCurrency { get; set; }

    [JsonProperty("mid")]
    public required double Value { get; set; }

    [JsonProperty("unit")]
    public int Unit { get; set; }

    [JsonProperty("timestamp")]
    public DateTime TimeStamp { get; set; }
}

public class ExchangeRateResponse
{
    [JsonProperty("status_code")]
    public int Status { get; set; }

    [JsonProperty("data")]
    public Rate? Rate { get; set; }
}
