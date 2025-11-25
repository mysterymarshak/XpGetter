using Spectre.Console;
using XpGetter.Errors;

namespace XpGetter.Extensions;

public static class BaseErrorExtensions
{
    public static void DumpToConsole(this BaseError error, string format)
    {
        AnsiConsole.MarkupLine(format);
        AnsiConsole.MarkupLine(error.Message);
        if (error.Exception is not null)
        {
            AnsiConsole.WriteException(error.Exception);
        }
    }

    public static void DumpToConsole(this BaseError error, string format, params object[] args)
    {
        AnsiConsole.MarkupLine(format, args);
        AnsiConsole.MarkupLine(error.Message);
        if (error.Exception is not null)
        {
            AnsiConsole.WriteException(error.Exception);
        }
    }
}
