using XpGetter.Responses;

namespace XpGetter.Results;

public record NoResultsOnPage(int Page, DateTimeOffset LastEntryDateTime, int ItemsScanned, CursorInfo Cursor);