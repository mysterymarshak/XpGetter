using System.ComponentModel;
using Autofac;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using XpGetter.Application;
using XpGetter.Application.Features.Configuration;
using XpGetter.Cli.States;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli;

internal sealed class RunCommand : AsyncCommand<RunCommand.Arguments>
{
    // TODO: add --prefer-personal-names
    // TODO: add --dont-use-currency-symbols
    // TODO: add --dont-encrypt-configuration
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
            containerBuilder.RegisterModule<ApplicationModule>();
            containerBuilder.RegisterModule<CliModule>();
            using var container = containerBuilder.Build();

            var statesResolver = container.Resolve<IStatesResolver>();
            var logger = container.Resolve<ILogger>();
            var context = new StateContext(statesResolver);

            logger.Debug(Messages.Start.HelloLog);

            var configurationService = container.Resolve<IConfigurationService>();
            var configuration = configurationService.GetConfiguration();
            var configurationParameter = new NamedParameter("configuration", configuration);

            var initialState = (BaseState)(arguments.SkipMenu switch
            {
                true => context.ResolveState<StartState>(configurationParameter),
                false => context.ResolveState<HelloState>(configurationParameter)
            });

            var result = await initialState.TransferControl();
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
            else if (result is ErrorExecutionResult errorExecutionResult)
            {
                errorExecutionResult.DumpError();

                WaitForAnyKeyToExit();
                return 1;
            }

            AnsiConsole.MarkupLine(Messages.Start.Exited);
            return 0;
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLine(Messages.Common.FatalError);
            AnsiConsole.WriteException(exception);

            WaitForAnyKeyToExit();

            return 1;
        }
    }

    private void WaitForAnyKeyToExit()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(Messages.Common.AnyKeyToExit);
        Console.ReadKey();
    }
}
