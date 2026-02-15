using Autofac.Core;
using XpGetter.Application.Dto;

namespace XpGetter.Cli.States;

public class StateContext
{
    public BaseState? State { get; private set; }
    public AppConfigurationDto Configuration { get; }

    private readonly IStatesResolver _statesResolver;

    public StateContext(IStatesResolver statesResolver,
                        AppConfigurationDto configuration)
    {
        _statesResolver = statesResolver;
        Configuration = configuration;
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
