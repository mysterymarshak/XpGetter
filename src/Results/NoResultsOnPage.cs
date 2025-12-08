using XpGetter.Steam.Http.Responses;

namespace XpGetter.Results;

public record NoResultsOnPage(int Page, DateTimeOffset LastEntryDateTime, int TotalItemsParsed, CursorInfo Cursor);
