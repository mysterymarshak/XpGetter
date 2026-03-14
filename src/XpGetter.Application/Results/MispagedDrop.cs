using XpGetter.Application.Dto;
using XpGetter.Application.Features.Steam.Http.Responses;

namespace XpGetter.Application.Results;

public record MispagedDrop(DateTimeOffset DateTime, Cs2Item FirstItem, CursorInfo? Cursor, IEnumerable<NewRankDrop> OthersOnThisPage);
