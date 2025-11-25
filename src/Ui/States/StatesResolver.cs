using Autofac;
using Autofac.Core;
using XpGetter.Dto;

namespace XpGetter.Ui.States;

public interface IStatesResolver
{
    T Resolve<T>(StateContext context, params IEnumerable<Parameter> parameters) where T : BaseState;
}

public class StatesResolver : IStatesResolver
{
    private readonly ILifetimeScope _container;

    public StatesResolver(ILifetimeScope container)
    {
        _container = container;
    }

    public T Resolve<T>(StateContext context, params IEnumerable<Parameter> parameters) where T : BaseState
    {
        return _container.Resolve<T>(parameters.Concat([new TypedParameter(typeof(StateContext), context)]));
    }
}
