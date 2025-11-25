using System.ComponentModel;
using Autofac;
using Spectre.Console;
using Spectre.Console.Cli;
using XpGetter.Configuration;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;
using XpGetter.Ui.States;

namespace XpGetter.Ui;

internal sealed class RunCommand : AsyncCommand<RunCommand.Arguments>
{
    public sealed class Arguments : CommandSettings
    {
        [CommandOption("--skip-menu")]
        [Description("Skips start menu")]
        [DefaultValue(false)]
        public bool SkipMenu { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext commandContext, Arguments arguments, CancellationToken cancellationToken)
    {
        try
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterModule<MainModule>();
            var container = containerBuilder.Build();

            var statesResolver = container.Resolve<IStatesResolver>();
            var configurationService = container.Resolve<IConfigurationService>();
            var context = new StateContext(statesResolver);
            var configuration = configurationService.GetConfiguration();

            StateExecutionResult result;
            if (arguments.SkipMenu)
            {
                var startState = context.ResolveState<StartState>(new NamedParameter("configuration", configuration));
                result = await startState.TransferControl();
            }
            else
            {
                var helloState = context.ResolveState<HelloState>(new NamedParameter("configuration", configuration));
                result = await helloState.TransferControl();
            }

            return result is PanicExecutionResult ? 1 : 0;
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLine(Messages.Common.FatalError);
            AnsiConsole.WriteException(exception);

            return 1;
        }
    }
}
