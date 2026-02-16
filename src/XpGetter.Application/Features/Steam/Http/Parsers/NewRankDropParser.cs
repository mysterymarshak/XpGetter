using HtmlAgilityPack;
using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Steam.Http.Responses;
using XpGetter.Application.Results;

namespace XpGetter.Application.Features.Steam.Http.Parsers;

public class NewRankDropParser
{
    public CursorInfo? Cursor { get; private set; }
    public int ParsedPages { get; private set; }
    public int ParsedItems { get; private set; }
    public DateTimeOffset? LastEntryDateTime { get; private set; }
    public bool ParseOnlyLastDrop { get; private set; }

    private const string NewRankDropRow1 = "Earned a new rank and got a drop";
    private const string NewRankDropRow2 = "Got an item drop";
    private const string MarketNameDefault = "<unknown>";

    private readonly ILogger _logger;

    private bool _skipNextNode;

    public NewRankDropParser(ILogger logger, bool parseOnlyLastDrop = true)
    {
        _logger = logger;
        ParseOnlyLastDrop = parseOnlyLastDrop;
    }

    public OneOf<IEnumerable<NewRankDrop>, NoResultsOnPage, MispagedDrop,
        NoDropHistoryFound, NewRankDropParserError> TryParseNext(InventoryHistoryResponse response)
    {
        if (response.Html is null)
        {
            return new NewRankDropParserError
            {
                Message = Messages.ActivityParsers.EmptyHtml
            };
        }

        var document = new HtmlDocument();
        document.LoadHtml(response.Html);

        var rows = document.DocumentNode.SelectNodes("//div[@class='tradehistoryrow']");
        if (rows is null)
        {
            // TODO: what if new account
            _logger.Error(Messages.ActivityParsers.Drop.NoHistoryRowsLog, response.Html);

            return new NewRankDropParserError
            {
                Message = Messages.ActivityParsers.Drop.NoHistoryRows
            };
        }

        ParsedPages++;
        Cursor = response.Cursor;

        var newRankDrops = new List<NewRankDrop>(ParseOnlyLastDrop ? 1 : 4);
        foreach (var (i, row) in rows.Index())
        {
            ParsedItems += 1;

            if (_skipNextNode)
            {
                _skipNextNode = false;
                continue;
            }

            var dropItems = new List<CsgoItem>();

            var parseDropItemResult = TryParseCsgoItemFromRow(response, row);
            if (parseDropItemResult.DateTime is null)
            {
                _logger.Warning(Messages.ActivityParsers.Drop.CannotParseDateTimeEntry, row.InnerHtml);
                continue;
            }

            LastEntryDateTime = parseDropItemResult.DateTime;

            if (parseDropItemResult.DropItem is null)
                continue;

            dropItems.Add(parseDropItemResult.DropItem);

            var nextRowIndex = i + 1;
            if (rows.Count == nextRowIndex)
            {
                return new MispagedDrop(LastEntryDateTime.Value, dropItems[0], response.Cursor, newRankDrops);
            }

            var nextDropItemNode = rows[nextRowIndex];
            var parseSecondDropItemResult = TryParseCsgoItemFromRow(response, nextDropItemNode);
            if (parseSecondDropItemResult.DateTime is null || parseSecondDropItemResult.DropItem is null)
            {
                _logger.Warning(Messages.ActivityParsers.Drop.CannotParseSecondItem,
                                row.InnerHtml, nextDropItemNode.InnerHtml);
            }
            else
            {
                dropItems.Add(parseSecondDropItemResult.DropItem);
            }

            _skipNextNode = true;
            newRankDrops.Add(new NewRankDrop(LastEntryDateTime.Value, dropItems));

            if (ParseOnlyLastDrop)
            {
                return newRankDrops;
            }
        }

        if (LastEntryDateTime is null || response.Cursor is null)
        {
            return new NoDropHistoryFound();
        }

        if (newRankDrops.Count == 0)
        {
            return new NoResultsOnPage(ParsedPages, LastEntryDateTime.Value, ParsedItems, Cursor);
        }

        return newRankDrops;
    }

    public OneOf<CsgoItem?, NewRankDropParserError> TryParseMispagedDrop(InventoryHistoryResponse response)
    {
        CsgoItem? result = null;

        var html = response.Html;
        if (html is null)
        {
            return new NewRankDropParserError
            {
                Message = Messages.ActivityParsers.Drop.EmptyMispagedDropHtml
            };
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var rows = document.DocumentNode.SelectNodes("//div[@class='tradehistoryrow']");
        if (rows is null or [])
        {
            _logger.Warning(Messages.ActivityParsers.Drop.NoHistoryRowsForMispagedDropLog, html);
            return result;
        }

        var row = rows.First();
        var parseDropItemResult = TryParseCsgoItemFromRow(response, row);
        if (parseDropItemResult.DateTime is null || parseDropItemResult.DropItem is null)
        {
            _logger.Warning(Messages.ActivityParsers.Drop.CannotParseMispagedDropLog, row.InnerHtml);
            return result;
        }

        _skipNextNode = true;
        result = parseDropItemResult.DropItem;

        return result;
    }

    private (CsgoItem? DropItem, DateTimeOffset? DateTime) TryParseCsgoItemFromRow(
        InventoryHistoryResponse response, HtmlNode row)
    {
        var parsedDateTime = TryParseDateTimeFromRow(row);
        if (parsedDateTime is null)
        {
            return (null, null);
        }

        var descriptionNode = row.SelectSingleNode(".//div[@class='tradehistory_event_description']");
        var innerTextTrimmed = descriptionNode?.InnerText.Trim();
        if (innerTextTrimmed is not (NewRankDropRow1 or NewRankDropRow2))
        {
            return (null, parsedDateTime);
        }

        var itemsGroupNode = row.SelectSingleNode(".//div[@class='tradehistory_items_group']");
        var earnedItemNode = itemsGroupNode?.SelectSingleNode(".//a[@class='history_item economy_item_hoverable']");
        if (earnedItemNode is null)
        {
            return (null, parsedDateTime);
        }

        var item = ExtractCsgoItemFromNode(response, earnedItemNode);
        if (item.MarketName == MarketNameDefault)
        {
            _logger.Warning(Messages.ActivityParsers.Drop.CannotParseMarketName, earnedItemNode.InnerHtml);
            return (null, parsedDateTime);
        }

        return (item, parsedDateTime);
    }

    private DateTimeOffset? TryParseDateTimeFromRow(HtmlNode row)
    {
        var dateNode = row.SelectSingleNode(".//div[@class='tradehistory_date']");
        var timestampNode = row.SelectSingleNode(".//div[@class='tradehistory_timestamp']");

        if (dateNode is null || timestampNode is null)
        {
            return null;
        }

        var dateText = dateNode.InnerText.Replace(timestampNode.InnerText, "").Trim();
        var timeText = timestampNode.InnerText.Trim();
        var dateTimeText = $"{dateText} {timeText}";

        if (!DateTimeOffset.TryParse(dateTimeText, out var parsedDateTime))
        {
            return null;
        }

        return parsedDateTime;
    }

    private CsgoItem ExtractCsgoItemFromNode(InventoryHistoryResponse response, HtmlNode node)
    {
        var attributes = node.Attributes;
        var classId = ulong.Parse(attributes["data-classid"].Value);
        var instanceId = ulong.Parse(attributes["data-instanceid"].Value);
        var nameNode = node.SelectSingleNode(".//span[@class='history_item_name']");
        var name = HtmlEntity.DeEntitize(nameNode?.InnerText ?? "<unknown>");
        var color = nameNode?.GetAttributeValue("style", string.Empty).Split("color: ")[1];
        var imgNode = node?.SelectSingleNode(".//img[@class='tradehistory_received_item_img']");
        var imgUrl = imgNode?.GetAttributeValue("src", null!);

        var description = GetItemDescription(response, classId, instanceId);
        var marketName = description?.MarketName ?? MarketNameDefault;
        var isMarketable = description?.Marketable ?? false;

        return new CsgoItem(
            name,
            marketName,
            isMarketable,
            imgUrl,
            color);
    }

    private ItemDescription? GetItemDescription(InventoryHistoryResponse response, ulong classId, ulong instanceId)
        => response.Descriptions?["730"][$"{classId}_{instanceId}"];
}
