using Spectre.Console.Cli;
using XpGetter.Cli;

var app = new CommandApp<RunCommand>();
return await app.RunAsync(args);