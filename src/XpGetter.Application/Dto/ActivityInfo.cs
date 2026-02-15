namespace XpGetter.Application.Dto;

public record ActivityInfo
{
    public required AccountDto Account { get; init; }
    public required NewRankDrop LastNewRankDrop { get; init; }
    public required MatchmakingData MatchmakingData { get; init; }
    public string? AdditionalMessage { get; init; }
}
