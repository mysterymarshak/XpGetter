using SteamKit2;
using XpGetter.Application.Dto;

namespace XpGetter.Application.Features.Configuration;

public static class RuntimeConfiguration
{
    public static bool CensorUsernames { get; set; }
    public static bool AnonymizeUsernames { get; set; }
    public static bool DontUseCurrencySymbols { get; set; }
    public static ECurrencyCode? ForceCurrency { get; set; } = null;
    public static PriceProvider PriceProvider { get; set; }
}
