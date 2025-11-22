using XpGetter.Dto;
using XpGetter.Responses;

namespace XpGetter.Results;

public record MispagedDrop(DateTimeOffset DateTime, DropItem FirstItem, CursorInfo? Cursor);