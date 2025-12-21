using QRCoder;
using Spectre.Console;
using System.Text;
using XpGetter.Application.Utils;

namespace XpGetter.Cli.Utils;

public class QrCode : IQrCode
{
    private int _lastHeight = 0;

    public void Draw(string url)
    {
        Render(url);
    }

    public void Clear()
    {
        if (_lastHeight > 0)
        {
            AnsiConsole.Cursor.MoveUp(_lastHeight);

            for (var i = 0; i < _lastHeight; i++)
            {
                AnsiConsole.Write("\u001b[2K");

                if (i < _lastHeight - 1)
                {
                    AnsiConsole.Cursor.MoveDown();
                }
            }

            AnsiConsole.Cursor.MoveUp(_lastHeight - 1);
        }
    }

    public void Reset()
    {
        _lastHeight = 0;
    }

    private void Render(string url)
    {
        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.L);
        var matrix = data.ModuleMatrix;
        var size = matrix.Count;

        var sb = new StringBuilder();
        for (var y = 0; y < size; y += 2)
        {
            sb.Append("    ");

            for (var x = 0; x < size; x++)
            {
                var top = matrix[y][x];
                var bottom = (y + 1 < size) && matrix[y + 1][x];

                if (top && bottom)       sb.Append('█');
                else if (top && !bottom) sb.Append('▀');
                else if (!top && bottom) sb.Append('▄');
                else                     sb.Append(' ');
            }

            sb.AppendLine();
        }

        AnsiConsole.Markup($"[white]{sb.ToString()}[/]");
        _lastHeight = (size + 1) / 2;
    }
}
