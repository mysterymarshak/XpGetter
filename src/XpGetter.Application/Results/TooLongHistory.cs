namespace XpGetter.Application.Results;

public record TooLongHistory(DateTimeOffset LastEntryDateTime, int ItemsScanned)
{
    public string Message => string.Format(Messages.Activity.TooLongHistory, ItemsScanned);
};