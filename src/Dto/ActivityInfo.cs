namespace XpGetter.Dto;

public record ActivityInfo
{
    public required string AccountName { get; set; }
    public required DateTimeOffset LastDropDateTime { get; set; }
    public string? LastKnownIpAddress { get; set; }
    public bool EarnedServiceMedal { get; set; }
    public int CsgoProfileRank { get; set; }
    public int ExperiencePointsToNextRank { get; set; }
    public TimeSpan AntiAddictionOnlineTime { get; set; }

    public bool IsDropAvailable()
    {
        var lastReset = GetLastDropResetTime();
        return lastReset > LastDropDateTime;
    }

    private static DateTimeOffset GetLastDropResetTime()
    {
        var utcNow = DateTime.UtcNow;
        var date = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 2, 0, 0, TimeSpan.Zero);

        var daysSinceWednesday = ((int)date.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7;
        if (daysSinceWednesday == 0 && utcNow.TimeOfDay < TimeSpan.FromHours(2))
        {
            daysSinceWednesday = 7;
        }

        return date.AddDays(-daysSinceWednesday);
    }
}
