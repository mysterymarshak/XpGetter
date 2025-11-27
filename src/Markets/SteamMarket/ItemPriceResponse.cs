using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace XpGetter.Markets.SteamMarket;

public partial class ItemPriceResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("lowest_price")]
    [JsonConverter(typeof(CurrencyConverter))]
    public double Value { get; set; }

    [JsonProperty("volume")]
    [JsonConverter(typeof(IntConverter))]
    public int? Volume { get; set; }

    private class IntConverter : JsonConverter<int?>
    {
        public override int? ReadJson(JsonReader reader, Type objectType, int? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var s = ((string?)reader.Value)?
                    .Replace(",", "")
                    .Replace(".", "");

                return s is null ? null : int.Parse(s, CultureInfo.InvariantCulture);
            }

            return Convert.ToInt32(reader.Value);
        }

        public override void WriteJson(JsonWriter writer, int? value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }

    private partial class CurrencyConverter : JsonConverter<double>
    {
        public override double ReadJson(JsonReader reader, Type objectType, double existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var raw = (string?)reader.Value;

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return 0;
                }

                var cleaned = RemovePrefixesSuffixesRegex().Replace(raw, "");
                return double.Parse(cleaned, CultureInfo.InvariantCulture);
            }

            return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
        }

        public override void WriteJson(JsonWriter writer, double value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }

        [GeneratedRegex(@"[^\d\.\-]")]
        private static partial Regex RemovePrefixesSuffixesRegex();
    }
}
