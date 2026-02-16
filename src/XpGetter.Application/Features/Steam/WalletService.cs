using OneOf;
using Serilog;
using SteamKit2;
using SteamKit2.Internal;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Configuration;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Steam;

public interface IWalletService
{
    Task<OneOf<WalletInfo, WalletServiceError>> GetWalletInfoAsync(SteamSession session, IProgressContext ctx);
}

public class WalletService : IWalletService
{
    private readonly ILogger _logger;

    public WalletService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<OneOf<WalletInfo, WalletServiceError>> GetWalletInfoAsync(SteamSession session, IProgressContext ctx)
    {
        var task = ctx.AddTask(session, Messages.Wallet.RetrievingWalletInfo);

        if (session.WalletInfo is null && RuntimeConfiguration.ForceCurrency is not null)
        {
            session.BindWalletInfo(new WalletInfo(RuntimeConfiguration.ForceCurrency.Value));
        }

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

            var currency = (ECurrencyCode)response.Body.currency_code;
            if (currency == ECurrencyCode.Invalid)
            {
                currency = ECurrencyCode.USD;
                _logger.Warning(Messages.Wallet.InvalidCurrencyLog, session.Account!.Username);
            }

            var walletInfo = new WalletInfo(currency);
            task.SetResult(session, Messages.Wallet.RetrievingWalletInfoOk);
            session.BindWalletInfo(walletInfo);

            return walletInfo;
        }
        catch (Exception exception)
        {
            task.SetResult(session, Messages.Wallet.RetrievingWalletInfoError);
            return new WalletServiceError
            {
                Message = Messages.Wallet.GetWalletInfoException.BindSession(session, logging: false),
                Exception = exception
            };
        }
    }
}
