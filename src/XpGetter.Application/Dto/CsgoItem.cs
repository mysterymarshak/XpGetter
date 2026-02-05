namespace XpGetter.Application.Dto;

public record CsgoItem(string Name, string MarketName, string? IconUrl, string? Color)
{
    public PriceDto? Price { get; private set; }

    public void BindPrice(PriceDto price)
    {
        Price = price;
    }

    public string? GetItemQuality()
    {
        return MarketName switch
        {
            _ when MarketName.Contains("Factory New") => "FN",
            _ when MarketName.Contains("Minimal Wear") => "MW",
            _ when MarketName.Contains("Field-Tested") => "FT",
            _ when MarketName.Contains("Well-Worn") => "WW",
            _ when MarketName.Contains("Battle-Scarred") => "BS",
            _ => null
        };
    }
}
