using OneOf;
using HtmlAgilityPack;
using Serilog;
using XpGetter.Dto;
using XpGetter.Errors;
using XpGetter.Results;

namespace XpGetter.Steam.Http.Responses.Parsers;

public class NewRankDropParser
{
    private const string NewRankDropRow = "Earned a new rank and got a drop";

    private readonly ILogger _logger;

    private int _parsedPages;
    private int _parsedItems;

    public NewRankDropParser(ILogger logger)
    {
        _logger = logger;
    }

    public OneOf<NewRankDrop, NoResultsOnPage, MispagedDrop, NoDropHistoryFound, NewRankDropParserError>
        TryParseNext(InventoryHistoryResponse response)
    {
        if (response.Html is null)
        {
            return new NewRankDropParserError
            {
                Message = Messages.ActivityParsers.Drop.EmptyHtml
            };
        }

        var document = new HtmlDocument();
        document.LoadHtml(response.Html);

        var rows = document.DocumentNode.SelectNodes("//div[@class='tradehistoryrow']");
        if (rows is null)
        {
            // TODO: what if new account
            _logger.Error(Messages.ActivityParsers.Drop.NoHistoryRowsLogger, response.Html);

            return new NewRankDropParserError
            {
                Message = Messages.ActivityParsers.Drop.NoHistoryRows
            };
        }

        var dropItems = new List<CsgoItem>();
        DateTimeOffset? lastEntryDateTime = null;

        foreach (var (i, row) in rows.Index())
        {
            var parseDropItemResult = TryParseCsgoItemFromRow(response, row);
            if (parseDropItemResult.DateTime is null)
            {
                _logger.Warning(Messages.ActivityParsers.Drop.CannotParseDateTimeEntry, row.InnerHtml);
                continue;
            }

            lastEntryDateTime = parseDropItemResult.DateTime;

            if (parseDropItemResult.DropItem is null)
                continue;

            dropItems.Add(parseDropItemResult.DropItem);

            var nextRowIndex = i + 1;
            if (rows.Count == nextRowIndex)
            {
                return new MispagedDrop(lastEntryDateTime.Value, dropItems[0], response.Cursor);
            }

            var nextDropItemNode = rows[i + 1];
            var parseSecondDropItemResult = TryParseCsgoItemFromRow(response, nextDropItemNode);
            if (parseSecondDropItemResult.DateTime is null || parseSecondDropItemResult.DropItem is null)
            {
                _logger.Warning(Messages.ActivityParsers.Drop.CannotParseSecondItem, row.InnerHtml);
            }
            else
            {
                dropItems.Add(parseSecondDropItemResult.DropItem);
            }

            return new NewRankDrop(lastEntryDateTime.Value, dropItems);
        }

        if (lastEntryDateTime is null || response.Cursor is null)
        {
            return new NoDropHistoryFound();
        }

        _parsedPages++;
        _parsedItems += response.EntriesCount;

        return new NoResultsOnPage(_parsedPages, lastEntryDateTime.Value, _parsedItems, response.Cursor);
    }

    public OneOf<CsgoItem?, NewRankDropParserError> TryParseMispagedDrop(InventoryHistoryResponse response)
    {
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
            _logger.Warning(Messages.ActivityParsers.Drop.NoHistoryRowsForMispagedDropLogger, html);

            return new NewRankDropParserError
            {
                Message = Messages.ActivityParsers.Drop.NoHistoryRowsForMispagedDrop
            };
        }

        var row = rows.First();
        var parseDropItemResult = TryParseCsgoItemFromRow(response, row);
        if (parseDropItemResult.DateTime is null || parseDropItemResult.DropItem is null)
        {
            _logger.Warning(Messages.ActivityParsers.Drop.CannotParseMispagedDropLogger, row.InnerHtml);

            return new NewRankDropParserError
            {
                Message = Messages.ActivityParsers.Drop.CannotParseMispagedDrop
            };
        }

        return parseDropItemResult.DropItem;
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
        if (descriptionNode?.InnerText.Trim() != NewRankDropRow)
        {
            return (null, parsedDateTime);
        }

        var itemsGroupNode = row.SelectSingleNode(".//div[@class='tradehistory_items_group']");
        var earnedItemNode = itemsGroupNode?.SelectSingleNode(".//a[@class='history_item economy_item_hoverable']");
        if (earnedItemNode is null)
        {
            return (null, parsedDateTime);
        }

        return (ExtractCsgoItemFromNode(response, earnedItemNode), parsedDateTime);
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

        return new CsgoItem(name, GetMarketName(response, classId, instanceId), imgUrl, color);
    }

    private string? GetMarketName(InventoryHistoryResponse response, ulong classId, ulong instanceId)
        => response.Descriptions?["730"][$"{classId}_{instanceId}"].MarketName;
}
