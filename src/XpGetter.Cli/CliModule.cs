using Autofac;
using XpGetter.Application.Utils;
using XpGetter.Cli.States;
using XpGetter.Cli.Utils;

namespace XpGetter.Cli;

public class CliModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<StatesResolver>()
            .As<IStatesResolver>()
            .SingleInstance();

        builder.RegisterAssemblyTypes(ThisAssembly)
            .AssignableTo<BaseState>()
            .AsSelf()
            .InstancePerDependency();

        builder.RegisterType<QrCode>()
            .As<IQrCode>()
            .InstancePerDependency();
    }
}
