using System.Text;
using XpGetter.Settings.Entities;

namespace XpGetter.Dto;

public record ActivityInfo
{
    public required Account Account { get; init; }
    public required NewRankDrop? LastNewRankDrop { get; init; }
    public required bool? ExternalIsDropAvailable { get; init; } = null;
    public required DateTimeOffset? ExternalDropWasNotObtainedAtLeastSince { get; init; } = null;
    public string? AdditionalMessage { get; init; } = null;
    public string? LastKnownIpAddress { get; set; }
    public bool EarnedServiceMedal { get; set; }
    public int CsgoProfileRank { get; set; }
    public int ExperiencePointsToNextRank { get; set; }
    public TimeSpan AntiAddictionOnlineTime { get; set; }
    
    public string GetPreviousLoot()
    {
        if (LastNewRankDrop is null or { Items: [] })
        {
            return "<unknown>";
        }

        var items = LastNewRankDrop.Items;
        var stringBuilder = new StringBuilder();
        
        var firstItem = items[0]; 
        AppendItem(firstItem, stringBuilder);

        if (items.Count == 1)
        {
            return stringBuilder.ToString();
        }

        var secondItem = items[1];
        stringBuilder.Append(" ; ");
        AppendItem(secondItem, stringBuilder);

        return stringBuilder.ToString();
        
        // TODO: find better place
    }

    private void AppendItem(DropItem item, StringBuilder stringBuilder)
    {
        stringBuilder.Append('[');
        stringBuilder.Append(item.Color ?? "silver");
        stringBuilder.Append(']');
        stringBuilder.Append(item.Name);
        stringBuilder.Append("[/]");
    }
    
    public string GetLastDropTime()
    {
        if (ExternalIsDropAvailable is true)
        {
            return "<unknown>";
        }

        if (ExternalDropWasNotObtainedAtLeastSince is not null)
        {
            return $"before {ExternalDropWasNotObtainedAtLeastSince.Value.LocalDateTime.ToShortDateString()}";
        }

        return LastNewRankDrop?.DateTime.ToString() ?? "<unknown>";
    }
    
    public bool? IsDropAvailable()
    {
        var lastReset = GetLastDropResetTime();
        if (ExternalIsDropAvailable.HasValue)
        {
            return ExternalIsDropAvailable.Value;
        }

        if (ExternalDropWasNotObtainedAtLeastSince.HasValue)
        {
            var assumption = lastReset > ExternalDropWasNotObtainedAtLeastSince.Value;
            if (assumption)
            {
                return true;
            }

            return null;
        }
        
        return lastReset > LastNewRankDrop?.DateTime;
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
