using HtmlAgilityPack;
using Newtonsoft.Json;
using OneOf;
using Serilog;
using XpGetter.Errors;
using XpGetter.Extensions;

namespace XpGetter.Steam.Http.Clients;

public interface ISteamHttpClient
{
    Task<OneOf<string, SteamHttpClientError>> GetAsync(string requestUri, string? sessionCookies = null);
    Task<OneOf<HtmlDocument, SteamHttpClientError>> GetHtmlAsync(string requestUri, string? sessionCookies = null);
    Task<OneOf<(T Deserialized, string Raw), SteamHttpClientError>> GetJsonAsync<T>(
        string requestUri, string? sessionCookies = null);
}

public class SteamHttpClient : ISteamHttpClient
{
    private const string BaseAddress = "https://steamcommunity.com";

    private readonly HttpClient _client;
    private readonly ILogger _logger;

    public SteamHttpClient(HttpClient httpClient, ILogger logger)
    {
        _client = httpClient;
        _client.BaseAddress = new Uri(BaseAddress);
        _logger = logger;
    }

    public async Task<OneOf<string, SteamHttpClientError>> GetAsync(string requestUri, string? sessionCookies = null)
    {
        try
        {
            var cookie = $"timezoneOffset={TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds},0";
            if (sessionCookies is not null)
            {
                cookie += $"; {sessionCookies}";
            }

            var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Cookie", cookie);

            var result = await _client.SendAsync(message);
            result.EnsureSuccessStatusCode();

            return await result.Content.ReadAsStringAsync();
        }
        catch (Exception exception)
        {
            return new SteamHttpClientError
            {
                Message = Messages.Http.Error,
                Exception = exception
            };
        }
    }

    public async Task<OneOf<(T Deserialized, string Raw), SteamHttpClientError>> GetJsonAsync<T>(
        string requestUri, string? sessionCookies = null)
    {
        try
        {
            var getResult = await GetAsync(requestUri, sessionCookies);
            if (getResult.TryPickT1(out var error, out var contentAsString))
            {
                return error;
            }

            var deserialized = JsonConvert.DeserializeObject<T>(contentAsString);
            if (deserialized is null)
            {
                var deserializationError = new SteamHttpClientError { Message = Messages.Http.DeserializationError };

                _logger.Error(Messages.Http.DeserializationErrorLog, contentAsString);
                return deserializationError;
            }

            return (deserialized, contentAsString);
        }
        catch (Exception exception)
        {
            return new SteamHttpClientError
            {
                Message = Messages.Http.Error,
                Exception = exception
            };
        }
    }

    public async Task<OneOf<HtmlDocument, SteamHttpClientError>> GetHtmlAsync(
        string requestUri, string? sessionCookies = null)
    {
        try
        {
            var getResult = await GetAsync(requestUri, sessionCookies);
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
            return new SteamHttpClientError
            {
                Message = Messages.Http.HtmlError,
                Exception = exception
            };
        }
    }
}
