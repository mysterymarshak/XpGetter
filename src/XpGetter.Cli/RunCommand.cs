using System.ComponentModel;
using Autofac;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using SteamKit2;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration;
using XpGetter.Cli.States;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli;

internal sealed class RunCommand : AsyncCommand<RunCommand.RuntimeArguments>
{
    // TODO: add --dont-encrypt-configuration
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

        [CommandOption("--dont-use-currency-symbols")]
        [Description("Uses 'USD' instead of '$' and so one")]
        [DefaultValue(false)]
        public bool DontUseCurrencySymbols { get; init; }

        [CommandOption("--currency")]
        [Description("Overrides currency in all price requests")]
        [DefaultValue(null)]
        public ECurrencyCode? Currency { get; init; }

        [CommandOption("--price-provider")]
        [Description("Changes the price provider")]
        [DefaultValue(PriceProvider.Steam)]
        public PriceProvider PriceProvider { get; init; }

        [CommandOption("--no-page-limit")]
        [Description("Ignores '{Constants.MaxInventoryHistoryPagesToLoad * 50}' (150) items limit and loads history up until the result. Use only if you really need that (e.g. if you made a lot of trades in the past and now you cannot load the year statistics)")]
        [DefaultValue(false)]
        public bool IgnorePageLimit { get; init; }

        public override ValidationResult Validate()
        {
            if (Currency == ECurrencyCode.Invalid)
            {
                return ValidationResult.Error("You cannot provide 'Invalid' currency.");
            }

            if (PriceProvider == PriceProvider.None)
            {
                return ValidationResult.Error("You cannot provide 'None' price provider.");
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
        RuntimeConfiguration.ForceCurrency = arguments.Currency;
        RuntimeConfiguration.PriceProvider = arguments.PriceProvider;
        RuntimeConfiguration.DontUseCurrencySymbols = arguments.DontUseCurrencySymbols;
        RuntimeConfiguration.IgnorePageLimit = arguments.IgnorePageLimit;
    }

    private void WaitForAnyKeyToExit()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(Messages.Common.AnyKeyToExit);
        Console.ReadKey();
    }
}
