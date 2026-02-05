using Newtonsoft.Json;
using OneOf;
using OneOf.Types;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Steam.Http.Clients;
using XpGetter.Application.Features.Steam.Http.Responses;
using XpGetter.Application.Results;

namespace XpGetter.Application.Features.Steam;

public interface IParentalService
{
    Task<OneOf<Success, InvalidPassword, ParentalServiceError>> UnlockFamilyViewAsync(
        SteamSession session, string password);
}

public class ParentalService : IParentalService
{
    private const string ParentalCookieName = "steamparental";

    private readonly ISteamHttpClient _httpClient;
    private readonly ILogger _logger;

    public ParentalService(ISteamHttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OneOf<Success, InvalidPassword, ParentalServiceError>> UnlockFamilyViewAsync(
        SteamSession session, string password)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(password), "pin");
        content.Add(new StringContent(session.Client.ID), "sessionid");

        using var response = await _httpClient.PostAsync(
            "parental/ajaxunlock/", session.AuthCookie, content);

        var contentAsString = await response.Content.ReadAsStringAsync();
        var deserialized = JsonConvert.DeserializeObject<UnlockFamilyViewResponse>(contentAsString);
        if (deserialized is null)
        {
            _logger.Error(Messages.Http.DeserializationErrorLog, contentAsString);
            return new ParentalServiceError { Message = Messages.Http.DeserializationError };
        }

        if (deserialized.Result == UnlockFamilyViewResponse.WrongPasswordResult)
        {
            return new InvalidPassword();
        }

        if (deserialized.Result == UnlockFamilyViewResponse.WrongPasswordResultButNotReally)
        {
            // when you already unlocked your session steam somewhy sends
            // the response like "success": false, result: 2, message: "nice attempt try again"
            // and idk wtf is going here
            return new Success();
        }

        if (!deserialized.Success)
        {
            _logger.Error(Messages.Parental.NotOkLog);
            _logger.Error(Messages.Parental.ResponseLogDetails, contentAsString, response.Headers);
            return new ParentalServiceError { Message = Messages.Parental.NotOk };
        }

        if (response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
        {
            foreach (var header in cookieHeaders)
            {
                var value = header.Split(';')[0];
                if (value.StartsWith(ParentalCookieName))
                {
                    session.BindParentalCookie(value);
                    return new Success();
                }
            }
        }

        _logger.Error(Messages.Parental.NoCookieLog);
        _logger.Error(Messages.Parental.ResponseLogDetails, contentAsString, response.Headers);
        return new ParentalServiceError { Message = Messages.Parental.NoCookie };
    }
}
