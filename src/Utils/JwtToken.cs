using System.IdentityModel.Tokens.Jwt;
using OneOf;
using OneOf.Types;

namespace XpGetter.Utils;

public static class JwtToken
{
    public static OneOf<DateTimeOffset, Error<string>> GetExpirationDate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;

        if (expClaim is null)
        {
            return new Error<string>("Invalid access token is provided.");
        }

        var expUnix = long.Parse(expClaim);
        return DateTimeOffset.FromUnixTimeSeconds(expUnix);
    }
}