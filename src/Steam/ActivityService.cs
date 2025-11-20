using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using OneOf;
using XpGetter.Dto;

namespace XpGetter.Steam;

public interface IActivityService
{
    Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(Account account);
}

public class ActivityServiceError
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

public class ActivityService : IActivityService
{
    private const string BaseAddress = "https://steamcommunity.com/";

    public async Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(Account account)
    {
        var tasks = new List<Task>
        {
            GetHtml($"/profiles/{account.Id}/gcpd/730?tab=accountmain", GetAuthCookie(account)),
            GetLastRankDropDateTime(account)
        };
        await Task.WhenAll(tasks);

        var getDocumentResult = ((Task<OneOf<HtmlDocument, ActivityServiceError>>)tasks[0]).Result;
        var getLastDropDateTimeResult = ((Task<OneOf<DateTimeOffset, ActivityServiceError>>)tasks[1]).Result;

        if (getDocumentResult.TryPickT1(out var error, out var document))
        {
            return error;
        }

        if (getLastDropDateTimeResult.TryPickT1(out error, out var lastDropDateTime))
        {
            return error;
        }

        return ParseActivityInfoFromHtml(account, document, lastDropDateTime);
    }

    private async Task<OneOf<DateTimeOffset, ActivityServiceError>> GetLastRankDropDateTime(Account account)
    {
        var getDocumentResult = await GetHtml($"/profiles/{account.Id}/inventoryhistory?l=english", GetAuthCookie(account));
        if (getDocumentResult.TryPickT1(out var error, out var document))
        {
            return error;
        }

        var rows = document.DocumentNode.SelectNodes("//div[@class='tradehistoryrow']");
        if (rows is null)
        {
            return new ActivityServiceError { Message = "Invalid html page retrieved for GetLastRankDropDateTime()" };
        }

        DateTimeOffset? lastDropDateTime = null;
        foreach (var row in rows)
        {
            var descriptionNode = row.SelectSingleNode(".//div[@class='tradehistory_event_description']");
            if (descriptionNode?.InnerText.Trim() == "Earned a new rank and got a drop")
            {
                var dateNode = row.SelectSingleNode(".//div[@class='tradehistory_date']");
                var timestampNode = row.SelectSingleNode(".//div[@class='tradehistory_timestamp']");

                if (dateNode is not null && timestampNode is not null)
                {
                    var dateText = dateNode.InnerText.Replace(timestampNode.InnerText, "").Trim();
                    var timeText = timestampNode.InnerText.Trim();
                    var dateTimeText = $"{dateText} {timeText}";

                    DateTimeOffset.TryParse(dateTimeText, out var parsedDateTime);
                    lastDropDateTime = parsedDateTime;
                    break;
                }
            }
        }

        if (lastDropDateTime is null)
        {
            // TODO: edge case
            return new ActivityServiceError { Message = "No last drop DateTime retrieved for GetLastRankDropDateTime()" };
        }

        return lastDropDateTime.Value;
    }

    private async Task<OneOf<HtmlDocument, ActivityServiceError>> GetHtml(string requestUri, string cookies)
    {
        try
        {
            var baseAddress = new Uri(BaseAddress);
            using var handler = new HttpClientHandler { UseCookies = true };
            using var client = new HttpClient(handler) { BaseAddress = baseAddress };

            var message = new HttpRequestMessage(HttpMethod.Get, requestUri);
            message.Headers.Add("Cookie",
                $"{cookies}; timezoneOffset={TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalSeconds},0");

            var result = await client.SendAsync(message);
            result.EnsureSuccessStatusCode();

            var contentAsString = await result.Content.ReadAsStringAsync();
            var document = new HtmlDocument();
            document.LoadHtml(contentAsString);

            return document;
        }
        catch (Exception exception)
        {
            return new  ActivityServiceError { Message = "An error occured in GetHtml()", Exception = exception };
        }
    }

    private OneOf<ActivityInfo, ActivityServiceError> ParseActivityInfoFromHtml(Account account, HtmlDocument document, DateTimeOffset lastDropDateTime)
    {
        var tables = document.DocumentNode
            .QuerySelectorAll("table.generic_kv_table");

        if (tables is null)
        {
            return new ActivityServiceError { Message = "Table with class 'generic_kv_table' not found." };
        }

        var activityInfo = new ActivityInfo { Account = account, LastDropDateTime = lastDropDateTime };
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
