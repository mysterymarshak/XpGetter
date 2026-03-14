namespace XpGetter.Application.Dto;

public record Cs2Item(string Name, string HashName, bool IsMarketable, string? Color)
{
    public PriceDto? Price { get; private set; }

    public void BindPrice(PriceDto price)
    {
        Price = price;
    }

    public string? GetItemQuality()
    {
        return HashName switch
        {
            _ when HashName.Contains("Factory New") => "FN",
            _ when HashName.Contains("Minimal Wear") => "MW",
            _ when HashName.Contains("Field-Tested") => "FT",
            _ when HashName.Contains("Well-Worn") => "WW",
            _ when HashName.Contains("Battle-Scarred") => "BS",
            _ => null
        };
    }
}
