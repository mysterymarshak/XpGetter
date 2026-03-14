using Spectre.Console;
using Spectre.Console.Rendering;
using SteamKit2;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Markets;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

// TODO: i should rewrite this
public class ChooseWeeklyDropToClaim : BaseState
{
    private const int RewardsToClaim = 2;

    private readonly RetrieveWeeklyDropExecutionResult _retrieveResult;
    private readonly SteamSession _session;
    private readonly IMarketService _marketService;

    public ChooseWeeklyDropToClaim(RetrieveWeeklyDropExecutionResult retrieveResult, SteamSession session,
        IMarketService marketService, StateContext context) : base(context)
    {
        _retrieveResult = retrieveResult;
        _session = session;
        _marketService = marketService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var cs2Items = await AnsiConsole.CreateProgressContext(async ansiConsoleCtx =>
        {
            var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);

            var task = ctx.AddTask(_session, Messages.Statuses.RetrievingItemsPrice);

            var items = _retrieveResult.AvailableItems;
            var prices = await _marketService.GetItemsPriceAsync(
                items.Where(x => x.IsTradeable).Select(x => x.HashName).Distinct().ToList(),
                _session.WalletInfo?.CurrencyCode ?? ECurrencyCode.USD,
                _session,
                ctx);

            var result = new List<Cs2Item>(4);
            var isWarning = false;
            foreach (var item in items)
            {
                var priceToBind = prices.FirstOrDefault(x => x.HashName == item.HashName);
                var cs2Item = new Cs2Item(item.Name, item.HashName, item.IsTradeable, item.RarityColor);

                if (priceToBind is not null)
                {
                    cs2Item.BindPrice(priceToBind);
                }

                if (priceToBind is null && item.IsTradeable)
                {
                    isWarning = true;
                }

                result.Add(cs2Item);
            }

            task.SetResult(isWarning ? Messages.Statuses.RetrievingItemsPriceWarning : Messages.Statuses.RetrievingItemsPriceOk);
            return result;
        });

        AnsiConsole.MarkupLine(Messages.Gc.ChooseTwoItems);

        var selectedItems = RunLiveMenu(cs2Items);
        var selectedInventoryItems = selectedItems?
            .Select((_, i) => _retrieveResult.AvailableItems[i])
            .ToList();

        return new ChooseWeeklyDropToClaimExecutionResult { SelectedItems = selectedInventoryItems };
    }

    private List<Cs2Item>? RunLiveMenu(List<Cs2Item> items)
    {
        var currentIndex = 0;
        var selectedIndices = new HashSet<int>();
        List<Cs2Item>? result = null;
        var exit = false;

        AnsiConsole.Cursor.Hide();

        var menu = new DynamicRenderable(() => RenderMenu(items, currentIndex, selectedIndices));

        AnsiConsole.Live(menu)
            .Start(ctx =>
            {
                ctx.Refresh();

                while (!exit)
                {
                    var key = Console.ReadKey(true);

                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            currentIndex = (currentIndex - 1 + items.Count) % items.Count;
                            break;
                        case ConsoleKey.DownArrow:
                            currentIndex = (currentIndex + 1) % items.Count;
                            break;
                        case ConsoleKey.Spacebar:
                            if (selectedIndices.Contains(currentIndex))
                            {
                                selectedIndices.Remove(currentIndex);
                            }
                            else if (selectedIndices.Count < RewardsToClaim)
                            {
                                selectedIndices.Add(currentIndex);
                            }

                            break;
                        case ConsoleKey.C:
                            selectedIndices.Clear();
                            break;
                        case ConsoleKey.Escape:
                            exit = true;
                            break;
                        case ConsoleKey.Enter:
                            if (selectedIndices.Count == RewardsToClaim)
                            {
                                exit = true;
                                result = selectedIndices.Select(i => items[i]).ToList();
                            }

                            break;
                    }

                    ctx.Refresh();
                }
            });

        AnsiConsole.Cursor.Show();
        return result;
    }

    private IRenderable RenderMenu(List<Cs2Item> items, int currentIndex, HashSet<int> selectedIndices)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap()); // > cursor
        grid.AddColumn(new GridColumn().NoWrap()); // [[ X ]] marker
        grid.AddColumn(new GridColumn()); // hashname
        grid.AddColumn(new GridColumn()); // price

        var limitReached = selectedIndices.Count == RewardsToClaim;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isHovered = i == currentIndex;
            var isSelected = selectedIndices.Contains(i);

            var cursor = isHovered ? "> " : "  ";
            var selectionMarker = isSelected ? "[[X]] " : "[[ ]] ";

            var textColor = Color.FromHex(item.Color ?? "#ded6cc");
            var style = new Style(foreground: textColor);

            if (limitReached && !isSelected)
            {
                style = style.Decoration(Decoration.Dim);
            }

            if (isSelected)
            {
                style = style.Decoration(Decoration.Bold | Decoration.Underline);
            }

            if (isHovered)
            {
                style = style.Decoration(Decoration.Invert);

                if (!isSelected && limitReached)
                {
                    style = style.Decoration(Decoration.Dim);
                }
            }

            var itemPrice = item.Price;
            var priceStyle = new Style(foreground: itemPrice is null ? Color.Wheat1 : Color.LightGreen);
            var priceString = itemPrice is null ? "-" : itemPrice.Currency.FormatValue(itemPrice.Value);
            if (!isHovered && !isSelected || (isHovered && !isSelected && limitReached))
            {
                priceStyle = priceStyle.Decoration(Decoration.Dim);
            }

            grid.AddRow(
                new Text(cursor, new Style(foreground: isHovered ? Color.White : Color.Black)),
                new Text(selectionMarker, style),
                new Text(item.HashName, style),
                new Text(priceString, priceStyle) { Justification = Justify.Center }
            );
        }

        grid.AddEmptyRow();

        var summaryStyle = new Style(foreground: Color.LightGreen);
        if (!limitReached)
        {
            summaryStyle = summaryStyle.Decoration(Decoration.Dim);
        }

        var summaryPrice =
            Math.Round(
                selectedIndices
                    .Select(x => items[x].Price)
                    .Where(x => x is not null)
                    .Sum(x => x!.Value), 2);

        var currency = items.First(x => x.Price?.Currency is not null).Price!.Currency;
        var summaryPriceString = currency.FormatValue(summaryPrice);

        grid.AddRow(
            new Text(""),
            new Text("Summary:"),
            new Text(""),
            new Text(summaryPriceString, summaryStyle));

        var enterColor = limitReached ? "lightgreen" : "grey";
        var escColor = limitReached ? "grey69" : "indianred_1";
        grid.AddRow(
            new Text(""),
            new Text(""),
            new Markup($"[grey]([grey69]Space[/] to toggle, [grey69]'c'[/] to clear, [{escColor}]Esc[/] to exit)[/] [{enterColor}](Enter to confirm)[/]")
        );

        return grid;
    }

    private class DynamicRenderable : IRenderable
    {
        private readonly Func<IRenderable> _resolveRenderable;

        public DynamicRenderable(Func<IRenderable> resolveRenderable)
        {
            _resolveRenderable = resolveRenderable;
        }

        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            return _resolveRenderable().Measure(options, maxWidth);
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            return _resolveRenderable().Render(options, maxWidth);
        }
    }
}
