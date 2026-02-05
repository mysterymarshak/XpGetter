using SteamKit2;

namespace XpGetter.Application.Dto;

public enum PriceProvider
{
    None = 0,
    MarketCsgo,
    Steam
}

public record PriceDto(string MarketName, double Value, string ProviderRaw, ECurrencyCode Currency)
{
    public PriceProvider Provider => ProviderRaw switch
    {
        "MarketCSGO" => PriceProvider.MarketCsgo,
        "Steam" => PriceProvider.Steam,
        _ => PriceProvider.None
    };
}
