using XpGetter.Dto;
using XpGetter.Steam.Http.Responses;

namespace XpGetter.Results;

public record MispagedDrop(DateTimeOffset DateTime, CsgoItem FirstItem, CursorInfo? Cursor);