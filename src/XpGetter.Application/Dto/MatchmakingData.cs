namespace XpGetter.Application.Dto;

public record XpData(int Rank, int Xp);

public record CooldownData(bool Exists, DateTimeOffset? ExpirationDate, int? CooldownLevel, bool? Acknowledged);

public record MatchmakingData(XpData XpData, CooldownData? CooldownData);
