namespace XpGetter.Results;

public record NoResultsOnPage(int Page, DateTimeOffset LastEntryDateTime, int TotalItemsParsed);