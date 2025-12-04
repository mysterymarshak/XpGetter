namespace XpGetter.Dto;

public record CsgoItem(string Name, string? MarketName, string? IconUrl, string? Color)
{
    public PriceDto? Price { get; private set; }

    public void BindPrice(PriceDto price)
    {
        Price = price;
    }
}