using Spectre.Console.Cli;
using XpGetter.Ui;

var app = new CommandApp<RunCommand>();
return await app.RunAsync(args);
