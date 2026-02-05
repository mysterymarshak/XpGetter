using SteamKit2;

namespace XpGetter.Application.Features.Configuration;

public static class RuntimeConfiguration
{
    public static bool CensorUsernames { get; set; }
    public static bool AnonymizeUsernames { get; set; }
    public static ECurrencyCode? ForceCurrency { get; set; } = null!;
}
