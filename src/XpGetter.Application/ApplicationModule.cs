using System.Net;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Activity;
using XpGetter.Application.Features.Activity.NewRankDrops;
using XpGetter.Application.Features.Configuration;
using XpGetter.Application.Features.Configuration.Repositories;
using XpGetter.Application.Features.Configuration.Repositories.FileOperationStrategies;
using XpGetter.Application.Features.ExchangeRates;
using XpGetter.Application.Features.ExchangeRates.ExchangeRateApi;
using XpGetter.Application.Features.ExchangeRates.HexaRateApi;
using XpGetter.Application.Features.Markets;
using XpGetter.Application.Features.Markets.CsgoMarket;
using XpGetter.Application.Features.Markets.SteamMarket;
using XpGetter.Application.Features.Steam;
using XpGetter.Application.Features.Steam.Http;
using XpGetter.Application.Features.Versioning;
using XpGetter.Application.Mappers;

namespace XpGetter.Application;

public class ApplicationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        const string logFileBaseName = "log.txt";
        var logger = new LoggerConfiguration()
            .WriteTo.File(Path.GetFilePathWithinExecutableDirectory(logFileBaseName), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3)
            .MinimumLevel.Debug()
            .CreateLogger();

        builder.RegisterInstance(logger)
            .As<ILogger>()
            .SingleInstance();

        builder.RegisterType<AuthenticationService>()
            .As<IAuthenticationService>()
            .SingleInstance();

        builder.RegisterType<ActivityService>()
            .As<IActivityService>()
            .SingleInstance();

        builder.RegisterType<SessionService>()
            .As<ISessionService>()
            .SingleInstance();

        builder.RegisterType<ConfigurationRepository>()
            .As<IConfigurationRepository>()
            .SingleInstance();

        var services = new ServiceCollection();

        services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(_ =>
            HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Min(Math.Pow(4, retryAttempt), 60)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        if (outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests &&
                            context.TryGetValue("OnRateLimit", out var action) && action is Action callback)
                        {
                            callback();
                        }
                    }));

        services.AddHttpClient<ISteamHttpClient, SteamHttpClient>()
            .AddPolicyHandler((sp, _) => sp.GetRequiredService<IAsyncPolicy<HttpResponseMessage>>());

        builder.Populate(services);

        builder.RegisterType<WalletService>()
            .As<IWalletService>()
            .SingleInstance();

        builder.RegisterType<CsgoMarketService>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<SteamMarketService>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<FallbackMarketService>()
            .As<IMarketService>()
            .SingleInstance();

        builder.RegisterType<ExchangeRateApiService>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<HexaRateApiService>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<FallbackExchangeRateService>()
            .As<IExchangeRateService>()
            .SingleInstance();

        builder.RegisterType<ConfigurationService>()
            .As<IConfigurationService>()
            .SingleInstance();

        builder.RegisterType<AccountMapper>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<ConfigurationMapper>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<NewRankDropsService>()
            .As<INewRankDropsService>()
            .SingleInstance();

        builder.RegisterDecorator<PricedNewRankDropsService, INewRankDropsService>();

        builder.RegisterType<EncryptedFileOperationStrategy>()
            .As<IFileOperationStrategy>()
            .SingleInstance();

        builder.RegisterType<VersioningService>()
            .As<IVersioningService>()
            .SingleInstance();

        builder.RegisterType<ParentalService>()
            .As<IParentalService>()
            .SingleInstance();

        builder.RegisterType<StatisticsService>()
            .As<IStatisticsService>()
            .SingleInstance();
    }
}
