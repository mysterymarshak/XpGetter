using Autofac.Core;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Cli.States;

public abstract class BaseState
{
    private readonly StateContext _context;

    public BaseState(StateContext context)
    {
        _context = context;
    }

    public abstract ValueTask<StateExecutionResult> OnExecuted();

    public async ValueTask<StateExecutionResult> TransferControl()
    {
        _context.SetState(this);
        return await OnExecuted();
    }

    public async ValueTask<StateExecutionResult> GoTo<T>(params IEnumerable<Parameter> parameters) where T : BaseState
    {
        var state = _context.ResolveState<T>(parameters);
        return await state.TransferControl();
    }
}
