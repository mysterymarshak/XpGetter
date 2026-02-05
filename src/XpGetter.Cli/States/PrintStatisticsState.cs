using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Extensions;
using XpGetter.Cli.States.Results;
using XpGetter.Cli.Utils;

namespace XpGetter.Cli.States;

public class PrintStatisticsState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly List<StatisticsDto> _statistics;

    public PrintStatisticsState(AppConfigurationDto configuration, List<StatisticsDto> statistics,
                                StateContext context) : base(context)
    {
        _configuration = configuration;
        _statistics = statistics;
    }

    public override ValueTask<StateExecutionResult> OnExecuted()
    {
        var tables = new List<Table>();
        var maxItems = _statistics.Max(x => x.GroupedItemsCount);
        var longestNameLength = _statistics
            .SelectMany(x => x.GroupedItems)
            .Max(x => x.Key.Name.Length);
        var dummyString = new string(' ', longestNameLength);

        foreach (var (i, statistics) in _statistics.Index())
        {
            var session = statistics.Session;
            var table = new Table()
                .Title(string.Format(Messages.Statistics.TableTitle, session.Account!.PersonalName))
                .AddColumn(Messages.Statistics.ItemNameColumn)
                .AddColumn(Messages.Statistics.ItemPriceColumn)
                .AddColumn(Messages.Statistics.ItemQuantityColumn);

            var currency = session.WalletInfo?.CurrencyCode;
            var total = statistics.GetTotalItemPrice();

            foreach (var group in statistics.GroupedItems)
            {
                table.AddRow($"[{group.Key.Color}]{group.Key.Name}[/]",
                             $"[green]{currency?.FormatValue(group.Key.Price?.Value ?? 0)}[/]",
                             $"x{group.Count()}");
            }

            table.Columns[0].Footer = new Text(Messages.Statistics.FooterTotal, new Style(foreground: Color.Yellow));
            // force setting the constant width to alignment purposes
            table.Columns[1].Width = 12;
            table.Columns[1].Footer = new Text(currency?.FormatValue(Math.Round(total, 2)) ?? string.Empty, new Style(foreground: Color.Green, decoration: Decoration.Bold));
            table.Columns[2].Footer = new Text(statistics.ItemsCount.ToString(), new Style(foreground: Color.Yellow));

            if (table.Rows.Count < maxItems)
            {
                var rowsToAdd = maxItems - table.Rows.Count;
                for (var j = 0; j < rowsToAdd; j++)
                {
                    table.AddRow(dummyString);
                }
            }
            tables.Add(table);
        }

        var columns = new Columns(tables)
            .Padding(2, 0, 2, 0)
            .Collapse();

        AnsiConsole.MarkupLine(string.Format(Messages.Statistics.Done, _statistics.First().TimeSpan.Days));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(columns);

        return ValueTask.FromResult<StateExecutionResult>(new SuccessExecutionResult());
    }
}
