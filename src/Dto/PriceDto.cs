using SteamKit2;

namespace XpGetter.Dto;

public enum PriceProvider
{
    None = 0,
    MarketCsgo,
    Steam,
    Unknown
}

public record PriceDto(string MarketName, double Value, string ProviderRaw, ECurrencyCode Currency)
{
    public PriceProvider Provider => ProviderRaw switch
    {
        "MarketCSGO" => PriceProvider.MarketCsgo,
        "Steam" => PriceProvider.Steam,
        _ => PriceProvider.Unknown
    };
}