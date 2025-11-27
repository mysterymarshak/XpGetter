using Autofac.Core;
using XpGetter.Cli.States;

namespace XpGetter.Dto;

public class StateContext
{
    public BaseState? State { get; private set; }

    private readonly IStatesResolver _statesResolver;

    public StateContext(IStatesResolver statesResolver)
    {
        _statesResolver = statesResolver;
    }

    public void SetState(BaseState state)
    {
        State = state;
    }

    public T ResolveState<T>(params IEnumerable<Parameter> parameters) where T : BaseState
    {
        return _statesResolver.Resolve<T>(this, parameters);
    }
}
