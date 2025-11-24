using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using OneOf;
using Serilog;
using XpGetter.Dto;
using XpGetter.Results;
using XpGetter.Settings.Entities;
using XpGetter.Steam.Http.Clients;
using XpGetter.Steam.Http.Responses;

namespace XpGetter.Steam.Services;

public interface IActivityService
{
    Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(Account account);
}

public class ActivityServiceError
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

// TODO: parsing logic is too heavy; needa extract
public class ActivityService : IActivityService
{
    private readonly ISteamHttpClient _steamHttpClient;
    private readonly ILogger _logger;

    public ActivityService(ISteamHttpClient steamHttpClient, ILogger logger)
    {
        _steamHttpClient = steamHttpClient;
        _logger = logger;
    }
    
    public async Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(Account account)
    {
        var tasks = new List<Task>
        {
            _steamHttpClient.GetHtmlAsync($"/profiles/{account.Id}/gcpd/730?tab=accountmain", GetAuthCookie(account)),
            GetLastNewRankDropAsync(account)
        };
        await Task.WhenAll(tasks);

        var getDocumentResult =
            ((Task<OneOf<HtmlDocument, ActivityServiceError>>)tasks[0]).Result;
        var getLastNewRankDropResult =
            ((Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, ActivityServiceError>>)tasks[1]).Result;

        if (getDocumentResult.TryPickT1(out var error, out var document))
        {
            return error;
        }

        if (getLastNewRankDropResult.TryPickT3(out error, out var remainder))
        {
            return error;
        }

        if (remainder.TryPickT0(out var lastNewRankDrop, out _) || remainder.IsT1 || remainder.IsT2)
        {
            var additionalMessage = lastNewRankDrop is null ? (
                remainder.IsT1 ? remainder.AsT1.Message : 
                remainder.IsT2 ? "No new rank drop info were found. Are you new in cs2?" : null) : null;
            return ParseActivityInfoFromHtml(account, document, lastNewRankDrop, remainder.IsT2 ? true : null,
                remainder.IsT1 ? remainder.AsT1.LastEntryDateTime : null, additionalMessage);
        }
        
        return new ActivityServiceError { Message = $"Impossible case. {nameof(GetActivityInfoAsync)}" };
    }

    private async Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, ActivityServiceError>>
        GetLastNewRankDropAsync(Account account, NoResultsOnPage? previousPageResult = null)
    {
        if (previousPageResult is { Page: > Constants.MaxInventoryHistoryPagesToLoad })
        {
            return new TooLongHistory(previousPageResult.LastEntryDateTime, previousPageResult.ItemsScanned);
        }
        
        var loadInventoryHistoryResult = await LoadInventoryHistoryAsync(account);
        if (loadInventoryHistoryResult.TryPickT1(out var error, out var result))
        {
            return error;
        }

        var page = (previousPageResult?.Page ?? 1) + 1;
        var previousItems = previousPageResult?.ItemsScanned ?? 0;
        var parseNewRankDropResult = TryParseNewRankDropFromInventoryHistory(
            page, previousItems, result.Deserialized);
        
        if (parseNewRankDropResult.TryPickT0(out var newRankDrop, out _))
        {
            return newRankDrop;
        }
        
        if (parseNewRankDropResult.TryPickT1(out var noResultsOnPage, out _))
        {
            return await GetLastNewRankDropAsync(account, noResultsOnPage);
        }

        if (parseNewRankDropResult.TryPickT2(out var mispagedDrop, out _))
        {
            if (mispagedDrop.Cursor is null)
            {
                _logger.Warning("Cannot retrieve mispaged drop if cursor is null.");
                return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem]);
            }
            
            loadInventoryHistoryResult = await LoadInventoryHistoryAsync(account, mispagedDrop.Cursor);
            if (loadInventoryHistoryResult.TryPickT1(out error, out result))
            {
                return error;
            }
            
            var parseMispagedDropResult = TryParseMispagedDrop(result.Deserialized.Html);
            if (parseMispagedDropResult.TryPickT1(out error, out var secondItem))
            {
                return error;
            }

            if (secondItem is not null)
            {
                return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem, secondItem]);
            }
            
            return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem]);
        }
        
        if (parseNewRankDropResult.TryPickT3(out _, out _))
        {
            return new NoDropHistoryFound();
        }

        if (parseNewRankDropResult.TryPickT4(out error, out _))
        {
            return error;
        }

        return new ActivityServiceError { Message = $"Impossible case. {nameof(GetLastNewRankDropAsync)}()" };
    }

    private OneOf<DropItem?, ActivityServiceError> TryParseMispagedDrop(string? html)
    {
        if (html is null)
        {
            return new ActivityServiceError
            {
                Message = $"Invalid html page retrieved for {nameof(TryParseMispagedDrop)}(). Was null"
            };
        }
        
        var document = new HtmlDocument();
        document.LoadHtml(html);
 
        var rows = document.DocumentNode.SelectNodes("//div[@class='tradehistoryrow']");
        if (rows is null)
        {
            return new ActivityServiceError
            {
                Message = $"Invalid html page retrieved for {nameof(TryParseMispagedDrop)}(). Raw: {html}"
            };
        }

        var row = rows.FirstOrDefault();
        if (row is null)
        {
            return new ActivityServiceError
            {
                Message = $"No inventory changes rows retrieved for {nameof(TryParseMispagedDrop)}(). Raw: {html}"
            };
        }
        
        var parseDropItemResult = TryParseDropItemFromRow(row);
        if (parseDropItemResult.DateTime is null || parseDropItemResult.DropItem is null)
        {
            _logger.Warning("Cannot parse second drop item.");
            _logger.Warning("Html: {InnerHtml}", row.InnerHtml);
            return (DropItem?)null;
        }

        return parseDropItemResult.DropItem;
    }
    
    private OneOf<NewRankDrop, NoResultsOnPage, MispagedDrop, NoDropHistoryFound, ActivityServiceError>
        TryParseNewRankDropFromInventoryHistory(int page, int previousItems, InventoryHistoryResponse response)
    {
        if (response.Html is null)
        {
            return new ActivityServiceError
            {
                Message = $"Invalid html page retrieved for {nameof(TryParseNewRankDropFromInventoryHistory)}(). Was null"
            };
        }
        
        var document = new HtmlDocument();
        document.LoadHtml(response.Html);
 
        var rows = document.DocumentNode.SelectNodes("//div[@class='tradehistoryrow']");
        if (rows is null)
        {
            return new ActivityServiceError
            {
                Message = $"Invalid html page retrieved for {nameof(TryParseNewRankDropFromInventoryHistory)}(). Raw: {response.Html}"
            };
        }
        
        var dropItems = new List<DropItem>();
        DateTimeOffset? lastEntryDateTime = null;
        
        foreach (var (i, row) in rows.Index())
        {
            var parseDropItemResult = TryParseDropItemFromRow(row);
            if (parseDropItemResult.DateTime is null)
            {
                _logger.Debug("Cannot parse datetime for entry.");
                _logger.Debug("Html: {InnerHtml}", row.InnerHtml);
                
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
            var parseSecondDropItemResult = TryParseDropItemFromRow(nextDropItemNode);
            if (parseSecondDropItemResult.DateTime is null || parseSecondDropItemResult.DropItem is null)
            {
                _logger.Warning("Cannot parse second drop item.");
                _logger.Warning("Html: {InnerHtml}", row.InnerHtml);
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
        
        return new NoResultsOnPage(page, lastEntryDateTime.Value, previousItems + response.EntriesCount, response.Cursor);
    }
    
    private async Task<OneOf<(InventoryHistoryResponse Deserialized, string Raw), ActivityServiceError>>
        LoadInventoryHistoryAsync(Account account, CursorInfo? cursor = null)
    {
        var queryString =
            $"l=english&ajax=1&cursor[time]={cursor?.Timestamp ?? 0}&cursor[time_frac]={cursor?.TimeFrac ?? 0}&cursor[s]={cursor?.CursorId ?? "0"}";
        var getJsonResult = await _steamHttpClient.GetJsonAsync<InventoryHistoryResponse>(
            $"/profiles/{account.Id}/inventoryhistory?{queryString}",
            GetAuthCookie(account));
        if (getJsonResult.TryPickT1(out var error, out var result))
        {
            return error;
        }
        
        if (!result.Deserialized.Success)
        {
            return new ActivityServiceError
            {
                Message = $"Success: false in {nameof(LoadInventoryHistoryAsync)}(). Raw response: {result.Raw}"
            };
        }

        return result;
    }
    
    private (DropItem? DropItem, DateTimeOffset? DateTime) TryParseDropItemFromRow(HtmlNode row)
    {
        var parsedDateTime = TryParseDateTimeFromRow(row);
        if (parsedDateTime is null)
        {
            return (null, null);
        }

        var descriptionNode = row.SelectSingleNode(".//div[@class='tradehistory_event_description']");
        if (descriptionNode?.InnerText.Trim() != "Earned a new rank and got a drop")
        {
            return (null, parsedDateTime);
        }
            
        var itemsGroupNode = row.SelectSingleNode(".//div[@class='tradehistory_items_group']");
        var earnedItemNode = itemsGroupNode?.SelectSingleNode(".//a[@class='history_item economy_item_hoverable']");
        if (earnedItemNode is null || earnedItemNode.GetAttributeValue("data-appid", 0) != 730)
        {
            return (null, parsedDateTime);
        }
        
        return (ExtractDropItemFromNode(earnedItemNode), parsedDateTime);
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

    private DropItem ExtractDropItemFromNode(HtmlNode node)
    {
        var attributes = node.Attributes;
        var classId = ulong.Parse(attributes["data-classid"].Value);
        var nameNode = node.SelectSingleNode(".//span[@class='history_item_name']");
        var name = HtmlEntity.DeEntitize(nameNode?.InnerText ?? "<unknown>");
        var color = nameNode?.GetAttributeValue("style", string.Empty).Split("color: ")[1];
        var imgNode = node?.SelectSingleNode(".//img[@class='tradehistory_received_item_img']");
        var imgUrl = imgNode?.GetAttributeValue("src", null!);
        
        return new DropItem(classId, name, imgUrl, color);
    }
    
    private OneOf<ActivityInfo, ActivityServiceError> ParseActivityInfoFromHtml(
        Account account, HtmlDocument document, NewRankDrop? lastNewRankDrop, bool? isDropAvailableForce = null,
        DateTimeOffset? dropWasNotObtainedSince = null, string? additionalMessage = null)
    {
        var tables = document.DocumentNode
            .QuerySelectorAll("table.generic_kv_table");

        if (tables is null)
        {
            return new ActivityServiceError { Message = "Table with class 'generic_kv_table' not found." };
        }

        var activityInfo = new ActivityInfo
        {
            Account = account,
            LastNewRankDrop = lastNewRankDrop,
            ExternalIsDropAvailable = isDropAvailableForce,
            ExternalDropWasNotObtainedAtLeastSince = dropWasNotObtainedSince,
            AdditionalMessage = additionalMessage
        };
        var lines = tables
            .Select(x => x.QuerySelectorAll(".generic_kv_line").Select(n => n.InnerText.Trim()))
            .SelectMany(x => x);

        foreach (var line in lines)
        {
            if (line.StartsWith("Last known IP address:"))
            {
                activityInfo.LastKnownIpAddress = line["Last known IP address:".Length..].Trim();
            }
            else if (line.StartsWith("Earned a Service Medal:"))
            {
                var value = line["Earned a Service Medal:".Length..].Trim();
                activityInfo.EarnedServiceMedal = value.Equals("Yes", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("CS:GO Profile Rank:"))
            {
                var value = line["CS:GO Profile Rank:".Length..].Trim();
                activityInfo.CsgoProfileRank = int.TryParse(value, out var rank) ? rank : 0;
            }
            else if (line.StartsWith("Experience points earned towards next rank:"))
            {
                var value = line["Experience points earned towards next rank:".Length..].Trim();
                activityInfo.ExperiencePointsToNextRank = int.TryParse(value, out var exp) ? exp : 0;
            }
            else if (line.StartsWith("Anti-addiction online time:"))
            {
                var timeStr = line["Anti-addiction online time:".Length..].Trim();
                if (TimeSpan.TryParse(timeStr, out var time))
                {
                    activityInfo.AntiAddictionOnlineTime = time;
                }
            }
        }

        return activityInfo;
    }

    private string GetAuthCookie(Account account) => $"steamLoginSecure={account.Id}||{account.AccessToken}";
}
