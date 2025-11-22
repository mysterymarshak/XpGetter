namespace XpGetter.Dto;

public record NewRankDrop(DateTimeOffset DateTime, IReadOnlyList<DropItem> Items);