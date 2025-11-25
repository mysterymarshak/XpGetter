using HtmlAgilityPack;
using Newtonsoft.Json;
using OneOf;
using XpGetter.Errors;

namespace XpGetter.Steam.Http.Clients;

public interface ISteamHttpClient
{
    Task<OneOf<string, ActivityServiceError>> GetAsync(string requestUri, string cookies);
    Task<OneOf<HtmlDocument, ActivityServiceError>> GetHtmlAsync(string requestUri, string cookies);
    Task<OneOf<(T Deserialized, string Raw), ActivityServiceError>> GetJsonAsync<T>(
        string requestUri, string cookies);
}

public class SteamHttpClient : ISteamHttpClient
{
    private const string BaseAddress = "https://steamcommunity.com/";

    private readonly HttpClient _client;

    public SteamHttpClient(HttpClient httpClient)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(BaseAddress);
    }

    public async Task<OneOf<string, ActivityServiceError>> GetAsync(string requestUri, string cookies)
    {
        try
        {
            var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Cookie",
                $"{cookies}; timezoneOffset={TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds},0");

            var result = await _client.SendAsync(message);
            result.EnsureSuccessStatusCode();

            return await result.Content.ReadAsStringAsync();
        }
        catch (Exception exception)
        {
            return new ActivityServiceError { Message = $"An error occured in {nameof(GetAsync)}()", Exception = exception };
        }
    }

    public async Task<OneOf<(T Deserialized, string Raw), ActivityServiceError>> GetJsonAsync<T>(
        string requestUri, string cookies)
    {
        try
        {
            var getResult = await GetAsync(requestUri, cookies);
            if (getResult.TryPickT1(out var error, out var contentAsString))
            {
                return error;
            }

            var deserialized = JsonConvert.DeserializeObject<T>(contentAsString);
            if (deserialized is null)
            {
                return new ActivityServiceError { Message = $"Cannot deserialize json. Raw: {contentAsString}" };
            }

            return (deserialized, contentAsString);
        }
        catch (Exception exception)
        {
            return new ActivityServiceError { Message = $"An error occured in {nameof(GetJsonAsync)}()", Exception = exception };
        }
    }

    public async Task<OneOf<HtmlDocument, ActivityServiceError>> GetHtmlAsync(string requestUri, string cookies)
    {
        try
        {
            var getResult = await GetAsync(requestUri, cookies);
            if (getResult.TryPickT1(out var error, out var contentAsString))
            {
                return error;
            }

            var document = new HtmlDocument();
            document.LoadHtml(contentAsString);

            return document;
        }
        catch (Exception exception)
        {
            return new ActivityServiceError { Message = $"An error occured in {nameof(GetHtmlAsync)}()", Exception = exception };
        }
    }
}
