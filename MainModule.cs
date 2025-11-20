using Autofac;
using Serilog;
using Serilog.Extensions.Autofac.DependencyInjection;
using XpGetter.Steam;

namespace XpGetter;

public class MainModule : Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterSerilog(new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug());

        builder.RegisterType<AuthorizationService>()
            .As<IAuthorizationService>()
            .SingleInstance();

        builder.RegisterType<ActivityService>()
            .As<IActivityService>()
            .SingleInstance();

        builder.RegisterType<SettingsProvider>()
            .AsSelf()
            .SingleInstance();
    }
}
