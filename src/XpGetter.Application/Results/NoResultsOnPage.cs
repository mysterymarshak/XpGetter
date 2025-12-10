using XpGetter.Application.Features.Steam.Http.Responses;

namespace XpGetter.Application.Results;

public record NoResultsOnPage(int Page, DateTimeOffset LastEntryDateTime, int TotalItemsParsed, CursorInfo Cursor);