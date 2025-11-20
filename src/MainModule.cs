using Autofac;
using Serilog;
using XpGetter.Steam;

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

        builder.RegisterType<AuthorizationService>()
            .As<IAuthorizationService>()
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
    }
}
