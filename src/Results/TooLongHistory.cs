namespace XpGetter.Results;

public record TooLongHistory(DateTimeOffset LastEntryDateTime, int ItemsScanned)
{
    public string Message =>
        $"Too long inventory history! Scanned '{ItemsScanned}' items and nothing about new rank drops were found.";
};