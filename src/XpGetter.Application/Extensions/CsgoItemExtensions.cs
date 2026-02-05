using System.Text;
using XpGetter.Application.Dto;

namespace XpGetter.Application.Extensions;

public static class CsgoItemExtensions
{
    private static readonly StringBuilder _builder = new();

    private const string DefaultColor = "silver";

    extension(CsgoItem item)
    {
        public string Format(bool includePrice = true)
        {
            item.FormatInternal(_builder, includePrice);

            var result = _builder.ToString();
            _builder.Clear();

            return result;
        }

        public void AddToStringBuilder(StringBuilder builder, bool includePrice = true)
        {
            item.FormatInternal(builder, includePrice);
        }

        private void FormatInternal(StringBuilder builder, bool includePrice)
        {
            builder.Append('[');
            builder.Append(item.Color ?? DefaultColor);
            builder.Append(']');
            builder.Append(item.Name);

            var quality = item.GetItemQuality();
            if (quality is not null)
            {
                builder.Append(" (");
                builder.Append(quality);
                builder.Append(')');
            }

            builder.Append("[/]");

            if (item.Price is not null && includePrice)
            {
                builder.Append(" [[");
                builder.Append("[green]");
                builder.Append(item.Price.Currency.FormatValue(item.Price.Value));
                builder.Append("[/]");
                builder.Append("]]");
            }
        }
    }
}
