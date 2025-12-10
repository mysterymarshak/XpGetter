using Autofac;
using XpGetter.Cli.States;

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
    }
}
