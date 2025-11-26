namespace XpGetter.Dto;

public enum PriceProvider
{
    None = 0,
    MarketCsgo,
    Steam,
    Unknown
}

public record PriceDto(string ItemName, double Value, string ProviderRaw)
{
    public PriceProvider Provider => ProviderRaw switch
    {
        "MarketCSGO" => PriceProvider.MarketCsgo,
        "Steam" => PriceProvider.Steam,
        _ => PriceProvider.Unknown
    };
}