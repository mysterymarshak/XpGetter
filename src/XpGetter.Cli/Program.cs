using Spectre.Console.Cli;
using XpGetter.Application;
using XpGetter.Cli;

var app = new CommandApp<RunCommand>();
app.Configure(config =>
{
    config.SetApplicationName(Constants.ProgramName);
    config.SetApplicationVersion(Constants.Version);

    config.AddExample("--skip-menu");
    config.AddExample("--anonymize", "--currency USD");
    config.AddExample("--censor false");
    config.AddExample("--price-provider MarketCsgo --currency RUB --dont-use-currency-symbols");
});

return await app.RunAsync(args);
