using SteamKit2;

namespace XpGetter.Application.Dto;

public record WalletInfo(bool? HasWallet, ECurrencyCode CurrencyCode);