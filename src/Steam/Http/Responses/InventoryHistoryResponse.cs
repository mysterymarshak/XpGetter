using Newtonsoft.Json;

namespace XpGetter.Steam.Http.Responses;

public class InventoryHistoryResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("html")]
    public string? Html { get; set; }

    [JsonProperty("num")]
    public int EntriesCount { get; set; }

    [JsonProperty("cursor")]
    public CursorInfo? Cursor { get; set; }

    [JsonProperty("descriptions")]
    public Dictionary<string, Dictionary<string, ItemDescription>>? Descriptions { get; set; }
}

public class ItemDescription
{
    [JsonProperty("market_hash_name")]
    public string? MarketName { get; set; }
}

public class CursorInfo
{
    [JsonProperty("time")]
    public long Timestamp { get; set; }

    [JsonProperty("time_frac")]
    public int TimeFrac { get; set; }

    [JsonProperty("s")]
    public string? CursorId { get; set; }
}
