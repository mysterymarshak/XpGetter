using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace XpGetter.Application.Features.Configuration.Entities;

// TODO: add IsDisabled
public class Account
{
    [JsonProperty("id")]
    public ulong Id { get; set; }

    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = null!;

    [JsonProperty("username")]
    public string Username { get; set; } = null!;

    [JsonProperty("family_view_pin")]
    public string? FamilyViewPin { get; set; }

    [JsonProperty("cache_data")]
    public AccountCacheData? CacheData { get; set; }

    public void Validate()
    {
        if (Id <= 0 || string.IsNullOrWhiteSpace(AccessToken) || string.IsNullOrWhiteSpace(RefreshToken) ||
            string.IsNullOrWhiteSpace(Username))
        {
            throw new ValidationException();
        }
    }
}
