using SteamKit2;

namespace XpGetter.Application.Dto;

public record ExchangeRateDto(ECurrencyCode Source, ECurrencyCode Target, double Value);
