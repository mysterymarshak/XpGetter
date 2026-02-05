using OneOf;
using SteamKit2;
using SteamKit2.Internal;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Steam;

public interface IWalletService
{
    Task<OneOf<WalletInfo, WalletServiceError>> GetWalletInfoAsync(SteamSession session, IProgressContext ctx);
}

public class WalletService : IWalletService
{
    public async Task<OneOf<WalletInfo, WalletServiceError>> GetWalletInfoAsync(SteamSession session, IProgressContext ctx)
    {
        var task = ctx.AddTask(session, Messages.Wallet.RetrievingWalletInfo);

        if (session.WalletInfo is not null)
        {
            task.SetResult(session, Messages.Wallet.RetrievingWalletInfoOk);
            return session.WalletInfo;
        }

        try
        {
            var request = new CUserAccount_GetClientWalletDetails_Request();
            var steamUnifiedMessages = session.Client.GetHandler<SteamUnifiedMessages>()!;
            var userAccountService = steamUnifiedMessages.CreateService<UserAccount>();
            var response = await userAccountService.GetClientWalletDetails(request);

            var walletInfo = new WalletInfo((ECurrencyCode)response.Body.currency_code);
            task.SetResult(session, Messages.Wallet.RetrievingWalletInfoOk);
            session.BindWalletInfo(walletInfo);

            return walletInfo;
        }
        catch (Exception exception)
        {
            task.SetResult(session, Messages.Wallet.RetrievingWalletInfoError);
            return new WalletServiceError
            {
                Message = Messages.Wallet.GetWalletInfoException.BindSession(session),
                Exception = exception
            };
        }
    }
}
