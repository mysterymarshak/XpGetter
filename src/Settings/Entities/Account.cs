using Newtonsoft.Json;

public class Account
{
    [JsonProperty("id")]
    public ulong Id { get; set; }

    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;
    
    [JsonProperty("personal_name")]
    public string PersonalName { get; set; } = string.Empty;
}
