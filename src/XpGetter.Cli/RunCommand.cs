using System.ComponentModel;
using Autofac;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using SteamKit2;
using XpGetter.Application;
using XpGetter.Application.Features.Configuration;
using XpGetter.Cli.States;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli;

internal sealed class RunCommand : AsyncCommand<RunCommand.RuntimeArguments>
{
    // TODO: add --dont-use-currency-symbols
    // TODO: add --dont-encrypt-configuration
    // TODO: add --price-provider (CSGO Market | Steam)
    public sealed class RuntimeArguments : CommandSettings
    {
        [CommandOption("--skip-menu")]
        [Description("Skips start menu")]
        [DefaultValue(false)]
        public bool SkipMenu { get; init; }

        [CommandOption("--censor")]
        [Description("Censors usernames in terminal output (logs are still uncensored)")]
        [DefaultValue(true)]
        public bool Censor { get; init; }

        [CommandOption("--anonymize")]
        [Description("Anonymizes all usernames in terminal output (logs are still unanonymized)")]
        [DefaultValue(false)]
        public bool Anonymize { get; init; }

        [CommandOption("--currency")]
        [Description("Override for currency to use in price requests")]
        [DefaultValue(null)]
        public string? Currency { get; init; }

        public override ValidationResult Validate()
        {
            if (Currency is not null)
            {
                var existing = Enum.GetNames<ECurrencyCode>().Any(x =>
                    x.Contains(Currency, StringComparison.InvariantCultureIgnoreCase));

                if (!existing)
                {
                    return ValidationResult.Error($"Currency not found: '{Currency}'");
                }
            }

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext commandContext,
                                                 RuntimeArguments arguments, CancellationToken cancellationToken)
    {
        try
        {
            InitializeRuntimeConfiguration(arguments);

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
            var skipToStartParameter = new NamedParameter("skipToStart", arguments.SkipMenu);
            var initialState = context.ResolveState<HelloState>(configurationParameter, skipToStartParameter);

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

    private void InitializeRuntimeConfiguration(RuntimeArguments arguments)
    {
        RuntimeConfiguration.AnonymizeUsernames = arguments.Anonymize;
        RuntimeConfiguration.CensorUsernames = arguments.Censor;

        if (arguments.Currency is not null)
        {
            RuntimeConfiguration.ForceCurrency = Enum.Parse<ECurrencyCode>(arguments.Currency, true);
        }
    }

    private void WaitForAnyKeyToExit()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(Messages.Common.AnyKeyToExit);
        Console.ReadKey();
    }
}
