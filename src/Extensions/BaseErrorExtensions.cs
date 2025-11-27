using Spectre.Console;
using XpGetter.Errors;

namespace XpGetter.Extensions;

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

        public void DumpToConsole(string format, params object[] args)
        {
            AnsiConsole.MarkupLine(format, args);
            AnsiConsole.MarkupLine(error.Message);
            if (error.Exception is not null)
            {
                AnsiConsole.WriteException(error.Exception);
            }
        }
    }
}
