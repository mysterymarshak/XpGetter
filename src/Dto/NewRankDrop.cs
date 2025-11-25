using System.Text;

namespace XpGetter.Dto;

public record NewRankDrop(DateTimeOffset? LastDateTime = null, IReadOnlyList<CsgoItem>? Items = null)
{
    private bool? _externalIsAvailable;
    private DateTimeOffset? _externalWasNotObtainedAtLeastSince;

    public void BindExternal(bool? isAvailable, DateTimeOffset? wasNotObtainedAtLeastSince)
    {
        _externalIsAvailable = isAvailable;
        _externalWasNotObtainedAtLeastSince = wasNotObtainedAtLeastSince;
    }

    public string GetPreviousLoot()
    {
        if (Items is [] or null)
        {
            return "<unknown>";
        }

        var stringBuilder = new StringBuilder();

        var firstItem = Items[0];
        AppendItem(firstItem, stringBuilder);

        if (Items.Count == 1)
        {
            return stringBuilder.ToString();
        }

        var secondItem = Items[1];
        stringBuilder.Append(" ; ");
        AppendItem(secondItem, stringBuilder);

        return stringBuilder.ToString();
    }

    private void AppendItem(CsgoItem item, StringBuilder stringBuilder)
    {
        stringBuilder.Append('[');
        stringBuilder.Append(item.Color ?? "silver");
        stringBuilder.Append(']');
        stringBuilder.Append(item.Name);
        stringBuilder.Append("[/]");
    }

    public string GetLastDropTime()
    {
        if (_externalIsAvailable is true)
        {
            return "<unknown>";
        }

        if (_externalWasNotObtainedAtLeastSince is not null)
        {
            return $"before {_externalWasNotObtainedAtLeastSince.Value.LocalDateTime.ToShortDateString()}";
        }

        return LastDateTime.ToString() ?? "<unknown>";
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

        return lastReset > LastDateTime;
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
