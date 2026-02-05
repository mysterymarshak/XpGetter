using Newtonsoft.Json;

namespace XpGetter.Application.Features.Steam.Http.Responses;

public class UnlockFamilyViewResponse
{
    public const int WrongPasswordResult = 15;
    // see Features/Steam/ParentalService.cs
    public const int WrongPasswordResultButNotReally = 2;

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("eresult")]
    public int Result { get; set; }

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }
}
