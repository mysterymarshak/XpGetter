using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using XpGetter.Settings;
using XpGetter.Steam;
using XpGetter.Steam.Http.Clients;
using XpGetter.Steam.Services;

namespace XpGetter;

public class MainModule : Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var logger = new LoggerConfiguration()
            .WriteTo.Console()
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

        builder.RegisterType<SettingsProvider>()
            .AsSelf()
            .SingleInstance();

        var services = new ServiceCollection();

        services.AddHttpClient<ISteamHttpClient, SteamHttpClient>()
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        
        builder.Populate(services);
    }
}
