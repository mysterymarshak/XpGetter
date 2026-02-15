using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class PrintStatisticsState : BaseState
{
    private readonly List<StatisticsDto> _statistics;

    public PrintStatisticsState(List<StatisticsDto> statistics, StateContext context) : base(context)
    {
        _statistics = statistics;
    }

    public override ValueTask<StateExecutionResult> OnExecuted()
    {
        var days = _statistics.First().TimeSpan.Days;
        var allGroupedItems = _statistics
            .SelectMany(x => x.GroupedItems);

        if (!allGroupedItems.Any())
        {
            AnsiConsole.MarkupLine(Messages.Statistics.NothingToShow, days);
            return ValueTask.FromResult<StateExecutionResult>(new SuccessExecutionResult());
        }

        var tables = new List<Table>();
        var maxItems = _statistics.Max(x => x.GroupedItemsCount);
        var longestNameLength = allGroupedItems
            .Max(x => x.Key.Format(includePrice: false, includeMarkup: false).Length);
        var dummyString = new string(' ', longestNameLength);

        foreach (var (i, statistics) in _statistics.Index())
        {
            var session = statistics.Session;
            var account = session.Account!;
            var table = new Table()
                .Title(string.Format(Messages.Statistics.TableTitle, $"[link=https://steamcommunity.com/profiles/{account.Id}]{account.GetDisplayPersonalName()}[/]"))
                .AddColumn(Messages.Statistics.ItemNameColumn)
                .AddColumn(Messages.Statistics.ItemPriceColumn)
                .AddColumn(Messages.Statistics.ItemQuantityColumn);

            var currency = session.WalletInfo?.CurrencyCode;
            var total = statistics.GetTotalItemPrice();

            foreach (var group in statistics.GroupedItems)
            {
                var nameColumn = $"[{group.Key.Color}]{group.Key.Format(includePrice: false)}[/]";
                var priceColumn = group.Key.IsMarketable
                    ? $"[green]{currency?.FormatValue(group.Key.Price?.Value ?? 0)}[/]"
                    : string.Empty;
                var quantityColumn = $"x{group.Count()}";
                table.AddRow(nameColumn, priceColumn, quantityColumn);
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

        AnsiConsole.MarkupLine(Messages.Statistics.Done, days);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(columns);

        return ValueTask.FromResult<StateExecutionResult>(new SuccessExecutionResult());
    }
}
