using Spectre.Console;
using Spectre.Console.Rendering;
using SteamKit2;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Cs2;
using XpGetter.Application.Features.Markets;
using XpGetter.Tui.Extensions;
using XpGetter.Tui.Progress;
using XpGetter.Tui.States.Results;

namespace XpGetter.Tui.States;

// TODO: i should rewrite this
public class ChooseWeeklyDropToClaimState : BaseState
{
    private const int RewardsToClaim = 2;

    private readonly RetrieveWeeklyDropExecutionResult _retrieveResult;
    private readonly SteamSession _session;
    private readonly IMarketService _marketService;

    public ChooseWeeklyDropToClaimState(RetrieveWeeklyDropExecutionResult retrieveResult, SteamSession session,
        IMarketService marketService, StateContext context) : base(context)
    {
        _retrieveResult = retrieveResult;
        _session = session;
        _marketService = marketService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var inventoryItems = _retrieveResult.AvailableItems;
        var marketItems = await AnsiConsole.CreateProgressContext(async ansiConsoleCtx =>
        {
            var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);

            var task = ctx.AddTask(_session, Messages.Statuses.RetrievingItemsPrice);

            var prices = await _marketService.GetItemsPriceAsync(
                inventoryItems.Where(x => x.IsTradeable).Select(x => x.HashName).Distinct().ToList(),
                _session.WalletInfo?.CurrencyCode ?? ECurrencyCode.USD,
                _session,
                ctx);

            var result = new List<Cs2Item>(4);
            var isWarning = false;
            foreach (var inventoryItem in inventoryItems)
            {
                var priceToBind = prices.FirstOrDefault(x => x.HashName == inventoryItem.HashName);
                var marketItem = new Cs2Item(inventoryItem.Name, inventoryItem.HashName, inventoryItem.IsTradeable,
                                          inventoryItem.RarityColor);

                if (priceToBind is not null)
                {
                    marketItem.BindPrice(priceToBind);
                }

                if (priceToBind is null && inventoryItem.IsTradeable)
                {
                    isWarning = true;
                }

                result.Add(marketItem);
            }

            task.SetResult(isWarning ? Messages.Statuses.RetrievingItemsPriceWarning : Messages.Statuses.RetrievingItemsPriceOk);
            return result;
        });

        AnsiConsole.MarkupLine(Messages.Gc.ChooseTwoItems);

        var weeklyDropItems = inventoryItems
            .Join(marketItems, x => x.HashName, y => y.HashName,
                  (x, y) => new WeeklyDropItem(x, y))
            .ToList();
        var selectedItems = RunLiveMenu(weeklyDropItems);
        var selectedInventoryItems = selectedItems?
            .Select(x => x.InventoryItem)
            .ToList();

        return new ChooseWeeklyDropToClaimExecutionResult { SelectedItems = selectedInventoryItems };
    }

    private List<WeeklyDropItem>? RunLiveMenu(List<WeeklyDropItem> items)
    {
        var currentIndex = 0;
        var selectedIndices = new HashSet<int>();
        List<WeeklyDropItem>? result = null;
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

    private IRenderable RenderMenu(List<WeeklyDropItem> items, int currentIndex, HashSet<int> selectedIndices)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap()); // > cursor
        grid.AddColumn(new GridColumn().NoWrap()); // [[ X ]] marker
        grid.AddColumn(new GridColumn()); // hashname
        grid.AddColumn(new GridColumn()); // float
        grid.AddColumn(new GridColumn()); // pattern
        grid.AddColumn(new GridColumn()); // price

        grid.AddRow(
            new Text(""),
            new Text(""),
            new Text("Market name"),
            new Text("Float"),
            new Text("Pattern"),
            new Text("Price"));
        grid.AddEmptyRow();

        var limitReached = selectedIndices.Count == RewardsToClaim;

        for (var i = 0; i < items.Count; i++)
        {
            var inventoryItem = items[i].InventoryItem;
            var marketItem = items[i].MarketItem;

            var isHovered = i == currentIndex;
            var isSelected = selectedIndices.Contains(i);

            var cursor = isHovered ? "> " : "  ";
            var selectionMarker = isSelected ? "[[X]] " : "[[ ]] ";

            var textColor = Color.FromHex(marketItem.Color ?? "#ded6cc");
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

            var itemPrice = marketItem.Price;
            var itemWear = inventoryItem.PaintKit?.Wear?.ToString() ?? "-";
            var itemPattern = inventoryItem.PaintKit?.Pattern.ToString() ?? "-";
            var priceStyle = new Style(foreground: itemPrice is null ? Color.Wheat1 : Color.LightGreen);
            var priceString = itemPrice is null ? "-" : itemPrice.Currency.FormatValue(itemPrice.Value);
            if (!isHovered && !isSelected || (isHovered && !isSelected && limitReached))
            {
                priceStyle = priceStyle.Decoration(Decoration.Dim);
            }

            grid.AddRow(
                new Markup(cursor, new Style(foreground: isHovered ? Color.White : Color.Black)),
                new Markup(selectionMarker, style),
                new Markup(marketItem.Format(includePrice: false), style),
                new Markup(itemWear, style),
                new Markup(itemPattern, style),
                new Markup(priceString, priceStyle) { Justification = Justify.Center }
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
                    .Select(x => items[x].MarketItem.Price)
                    .Where(x => x is not null)
                    .Sum(x => x!.Value), 2);

        var currency = items.First(x => x.MarketItem.Price?.Currency is not null).MarketItem.Price!.Currency;
        var summaryPriceString = currency.FormatValue(summaryPrice);

        grid.AddRow(
            new Text(""),
            new Text("Summary:"),
            new Text(""),
            new Text(""),
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

    private record WeeklyDropItem(Cs2InventoryItem InventoryItem, Cs2Item MarketItem);
}
