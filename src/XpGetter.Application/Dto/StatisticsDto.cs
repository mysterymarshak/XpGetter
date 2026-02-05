using SteamKit2;

namespace XpGetter.Application.Dto;

public record StatisticsDto(IEnumerable<NewRankDrop> NewRankDrops)
{
    public required SteamSession Session { get; init; }
    public required TimeSpan TimeSpan { get; init; }

    public int ItemsCount => field = field > 0 ? field : (field = NewRankDrops.SelectMany(x => x.Items!).Count());
    public IEnumerable<IGrouping<CsgoItem, CsgoItem>> GroupedItems => field ??= NewRankDrops
                .SelectMany(x => x.Items!)
                .OrderByDescending(x => x?.Price?.Value)
                .GroupBy(x => x, new CsgoItemByNameComparer());
    public int GroupedItemsCount => field = field > 0 ? field : (field = GroupedItems.Count());

    public double GetTotalItemPrice()
    {
        return NewRankDrops.SelectMany(x => x.Items!.Select(y => y.Price?.Value ?? 0)).Aggregate((x, y) => x + y);
    }
}
