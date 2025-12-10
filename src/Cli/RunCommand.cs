using System.ComponentModel;
using Autofac;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using XpGetter.Cli.States;
using XpGetter.Configuration;
using XpGetter.Dto;
using XpGetter.Extensions;
using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Cli;

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
            var logger = container.Resolve<ILogger>();
            var context = new StateContext(statesResolver);
            var configuration = configurationService.GetConfiguration();

            logger.Debug(Messages.Start.HelloLog);

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

            if (result is PanicExecutionResult panicExecutionResult)
            {
                panicExecutionResult.DumpError();
                if (!string.IsNullOrWhiteSpace(panicExecutionResult.Message))
                {
                    AnsiConsole.MarkupLine(panicExecutionResult.Message);
                }
                AnsiConsole.MarkupLine(Messages.Common.FatalError);

                return 1;
            }

            return 0;
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLine(Messages.Common.FatalError);
            AnsiConsole.WriteException(exception);

            return 1;
        }
    }
}
