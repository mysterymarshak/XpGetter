using System.Text;
using XpGetter.Application.Dto;

namespace XpGetter.Application.Extensions;

public static class CsgoItemExtensions
{
    private static readonly StringBuilder _builder = new();

    private const string DefaultColor = "silver";

    extension(CsgoItem item)
    {
        public string Format(bool includePrice = true, bool includeMarkup = true)
        {
            item.FormatInternal(_builder, includePrice, includeMarkup);

            var result = _builder.ToString();
            _builder.Clear();

            return result;
        }

        public void AddToStringBuilder(StringBuilder builder, bool includePrice = true, bool includeMarkup = true)
        {
            item.FormatInternal(builder, includePrice, includeMarkup);
        }

        private void FormatInternal(StringBuilder builder, bool includePrice, bool includeMarkup)
        {
            if (includeMarkup) builder.Append('[');
            builder.Append(item.Color ?? DefaultColor);
            if (includeMarkup) builder.Append(']');
            builder.Append(item.Name);

            var quality = item.GetItemQuality();
            if (quality is not null)
            {
                builder.Append(" (");
                builder.Append(quality);
                builder.Append(')');
            }

            if (includeMarkup) builder.Append("[/]");

            if (item.Price is not null && includePrice)
            {
                if (includeMarkup)
                {
                    builder.Append(" [[");
                    builder.Append("[green]");
                }

                builder.Append(item.Price.Currency.FormatValue(item.Price.Value));

                if (includeMarkup)
                {
                    builder.Append("[/]");
                    builder.Append("]]");
                }
            }
        }
    }
}
