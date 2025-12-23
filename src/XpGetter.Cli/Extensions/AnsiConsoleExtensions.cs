using Spectre.Console;

namespace XpGetter.Cli.Extensions;

public static class AnsiConsoleExtensions
{
    extension(AnsiConsole)
    {
        public static Task<T> CreateProgressContext<T>(Func<ProgressContext, Task<T>> func)
        {
            return AnsiConsole
                .Progress()
                .AutoRefresh(true)
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(new TaskDescriptionColumn { Alignment = Justify.Left }, new SpinnerColumn(Spinner.Known.Flip),
                    new ElapsedTimeColumn())
                .StartAsync(func);
        }
    }
}