using Spectre.Console;

namespace XpGetter.Cli.Utils;

public static class ProgressBar
{
    public static void Print(int value1, int value2)
    {
        var percentage = (float)value1/value2;
        var charactersCount = 20 + (4 - value1.ToString().Length);
        const char fillCharacter = '-';
        var coloredCharacters = (int)Math.Round(charactersCount * percentage);
        AnsiConsole.MarkupLine($"[[[green]{new string(fillCharacter, coloredCharacters)}[/]{value1}/{value2}{new string(fillCharacter, (charactersCount - coloredCharacters))}]]");
    }
}
