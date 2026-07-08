using Autofac;
using XpGetter.Tui.States;

namespace XpGetter.Tui;

public class TuiModule : Module
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
