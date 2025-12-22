using Spectre.Console;
using XpGetter.Application.Errors;

namespace XpGetter.Cli.Extensions;

public static class BaseErrorExtensions
{
    extension(BaseError error)
    {
        public void DumpToConsole(string message)
        {
            AnsiConsole.MarkupLine(message);
            AnsiConsole.MarkupLine(error.Message);
            if (error.Exception is not null)
            {
                AnsiConsole.WriteException(error.Exception);
            }
        }

        public void DumpToConsole(string format, string message)
        {
            AnsiConsole.MarkupLine(format, message);
            AnsiConsole.MarkupLine(error.Message);
            if (error.Exception is not null)
            {
                AnsiConsole.WriteException(error.Exception);
            }
        }
    }
}
