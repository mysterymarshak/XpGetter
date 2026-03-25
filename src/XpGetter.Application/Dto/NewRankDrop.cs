using System.Text;
using XpGetter.Application.Extensions;

namespace XpGetter.Application.Dto;

public record NewRankDrop(DateTimeOffset? LastDateTime, IReadOnlyList<Cs2Item> Items)
{
    private const string DefaultName = "<unknown>";
    private const string DefaultDate = "<unknown>";
    private const string ExternalDateFormat = "before {0}";

    private bool? _externalIsAvailable;
    private DateTimeOffset? _externalWasNotObtainedAtLeastSince;

    public void BindExternal(bool? isAvailable, DateTimeOffset? wasNotObtainedAtLeastSince)
    {
        _externalIsAvailable = isAvailable;
        _externalWasNotObtainedAtLeastSince = wasNotObtainedAtLeastSince;
    }

    public string GetPreviousLoot()
    {
        if (Items is [])
        {
            return DefaultName;
        }

        var stringBuilder = new StringBuilder();

        var firstItem = Items[0];
        firstItem.AddToStringBuilder(stringBuilder);

        if (Items.Count == 1)
        {
            return stringBuilder.ToString();
        }

        var secondItem = Items[1];
        stringBuilder.Append(" ; ");
        secondItem.AddToStringBuilder(stringBuilder);

        return stringBuilder.ToString();
    }

    public string GetLastDropTime()
    {
        if (_externalIsAvailable is true)
        {
            return DefaultDate;
        }

        if (_externalWasNotObtainedAtLeastSince is not null)
        {
            return string.Format(ExternalDateFormat, _externalWasNotObtainedAtLeastSince.Value.LocalDateTime.ToShortDateString());
        }

        return LastDateTime?.ToString("dd.MM.yyyy HH:mm") ?? DefaultDate;
    }

    public bool? IsDropAvailable()
    {
        var lastReset = GetLastDropResetTime();
        if (_externalIsAvailable.HasValue)
        {
            return _externalIsAvailable.Value;
        }

        if (_externalWasNotObtainedAtLeastSince.HasValue)
        {
            var assumption = lastReset > _externalWasNotObtainedAtLeastSince.Value;
            if (assumption)
            {
                return true;
            }

            return null;
        }
        else
        {
            if (!LastDateTime.HasValue)
            {
                return null;
            }
        }

        return lastReset > LastDateTime;
    }

    private static DateTimeOffset GetLastDropResetTime()
    {
        var utcNow = DateTime.UtcNow;
        var hoursOffset = new TimeSpan(1, 0, 0); // every Wednesday at 1:00 UTC
        var date = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, hoursOffset);

        var daysSinceWednesday = ((int)date.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7;
        if (daysSinceWednesday == 0 && utcNow.TimeOfDay < hoursOffset)
        {
            daysSinceWednesday = 7;
        }

        return date.AddDays(-daysSinceWednesday);
    }
}
