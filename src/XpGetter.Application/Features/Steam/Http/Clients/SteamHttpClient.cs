using HtmlAgilityPack;
using Newtonsoft.Json;
using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;

namespace XpGetter.Application.Features.Steam.Http.Clients;

public interface ISteamHttpClient
{
    Task<OneOf<HtmlDocument, SteamHttpClientError>> GetHtmlAsync(string requestUri, AuthCookie? authCookie = null);
    Task<OneOf<(T Deserialized, string Raw), SteamHttpClientError>> GetJsonAsync<T>(
        string requestUri, AuthCookie? authCookie = null);
    Task<HttpResponseMessage> PostAsync(string requestUri, AuthCookie authCookie, HttpContent content);
    Task<OneOf<string, SteamHttpClientError>> GetAsync(string requestUri, AuthCookie? authCookie = null);
}

// TODO: handle enabled family view (now it could fail with 403)
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

    public async Task<OneOf<(T Deserialized, string Raw), SteamHttpClientError>> GetJsonAsync<T>(
        string requestUri, AuthCookie? authCookie = null)
    {
        try
        {
            var getResult = await GetAsync(requestUri, authCookie);
            if (getResult.TryPickT1(out var error, out var contentAsString))
            {
                return error;
            }

            var deserialized = JsonConvert.DeserializeObject<T>(contentAsString);
            if (deserialized is null)
            {
                _logger.Error(Messages.Http.DeserializationErrorLog, contentAsString);
                return new SteamHttpClientError { Message = Messages.Http.DeserializationError };
            }

            return (deserialized, contentAsString);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, Messages.Http.ErrorLog);

            return new SteamHttpClientError
            {
                Message = Messages.Http.Error,
                Exception = exception
            };
        }
    }

    public async Task<OneOf<HtmlDocument, SteamHttpClientError>> GetHtmlAsync(
        string requestUri, AuthCookie? authCookie = null)
    {
        try
        {
            var getResult = await GetAsync(requestUri, authCookie);
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
            _logger.Error(exception, Messages.Http.ErrorLog);

            return new SteamHttpClientError
            {
                Message = Messages.Http.HtmlError,
                Exception = exception
            };
        }
    }

    public async Task<HttpResponseMessage> PostAsync(
        string requestUri, AuthCookie authCookie, HttpContent content)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, requestUri);
        message.Content = content;
        message.Headers.Add("Cookie", authCookie.ToString());

        var result = await _client.SendAsync(message);
        result.EnsureSuccessStatusCode();

        return result;
    }

    public async Task<OneOf<string, SteamHttpClientError>> GetAsync(
        string requestUri, AuthCookie? authCookie = null)
    {
        try
        {
            var cookie = $"timezoneOffset={TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds},0";
            if (authCookie is not null)
            {
                cookie += $"; {authCookie}";
            }

            using var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Cookie", cookie);

            using var result = await _client.SendAsync(message);
            result.EnsureSuccessStatusCode();

            return await result.Content.ReadAsStringAsync();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, Messages.Http.ErrorLog);

            return new SteamHttpClientError
            {
                Message = Messages.Http.Error,
                Exception = exception
            };
        }
    }
}
