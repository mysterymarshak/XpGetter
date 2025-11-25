using OneOf;
using SteamKit2;
using SteamKit2.Internal;
using XpGetter.Dto;
using XpGetter.Errors;

namespace XpGetter.Steam.Services;

public interface IWalletService
{
    Task<OneOf<WalletInfo, WalletServiceError>> GetWalletInfoAsync(SteamSession session);
}

public class WalletService : IWalletService
{
    public async Task<OneOf<WalletInfo, WalletServiceError>> GetWalletInfoAsync(SteamSession session)
    {
        try
        {
            var request = new CUserAccount_GetClientWalletDetails_Request();
            var steamUnifiedMessages = session.Client.GetHandler<SteamUnifiedMessages>()!;
            var userAccountService = steamUnifiedMessages.CreateService<UserAccount>();
            var response = await userAccountService.GetClientWalletDetails(request);

            return new WalletInfo((ECurrencyCode)response.Body.currency_code);
        }
        catch (Exception exception)
        {
            return new WalletServiceError
            {
                Message = "An error occured while retrieving wallet info.",
                Exception = exception
            };
        }
    }
}
