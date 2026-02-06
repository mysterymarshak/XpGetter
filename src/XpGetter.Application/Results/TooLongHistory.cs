using XpGetter.Application.Dto;

namespace XpGetter.Application.Results;

public record TooLongHistory(DateTimeOffset LastEntryDateTime, int ItemsScanned, IReadOnlyList<NewRankDrop> RetrievedNewRankDrops)
{
    public string Message => string.Format(Messages.Activity.TooLongHistory, ItemsScanned);
};
