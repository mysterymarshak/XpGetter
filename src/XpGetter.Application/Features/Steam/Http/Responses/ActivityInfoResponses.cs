using Newtonsoft.Json;

namespace XpGetter.Application.Features.Steam.Http.Responses;

public class ActivityInfoResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("html")]
    public string? Html { get; set; }
}