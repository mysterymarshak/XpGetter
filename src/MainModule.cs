using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using XpGetter.Configuration;
using XpGetter.Configuration.Repositories;
using XpGetter.Mappers;
using XpGetter.Markets;
using XpGetter.Markets.CsgoMarket;
using XpGetter.Markets.SteamMarket;
using XpGetter.Steam.Http.Clients;
using XpGetter.Steam.Services;
using XpGetter.Ui.States;

namespace XpGetter;

public class MainModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var logger = new LoggerConfiguration()
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3)
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
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.AddHttpClient<ISteamHttpClient, SteamHttpClient>()
            .AddPolicyHandler((sp, _) => sp.GetRequiredService<IAsyncPolicy<HttpResponseMessage>>());

        builder.Populate(services);

        builder.RegisterType<WalletService>()
            .As<IWalletService>()
            .SingleInstance();

        builder.RegisterType<CsgoMarketService>()
            .As<IMarketService>()
            .SingleInstance();

        builder.RegisterType<SteamMarketService>()
            .As<IMarketService>()
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

        builder.RegisterType<StatesResolver>()
            .As<IStatesResolver>()
            .SingleInstance();

        builder.RegisterAssemblyTypes(ThisAssembly)
            .AssignableTo<BaseState>()
            .AsSelf()
            .InstancePerDependency();
    }
}
