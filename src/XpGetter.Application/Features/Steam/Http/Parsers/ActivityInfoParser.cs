using HtmlAgilityPack;
using OneOf;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Steam.Http.Responses;

namespace XpGetter.Application.Features.Steam.Http.Parsers;

public class ActivityInfoParser
{
    private const string RankRow = "CS:GO Profile Rank:";
    private const string XpRow = "Experience points earned towards next rank:";

    private const string CooldownExpirationColumn = "Competitive Cooldown Expiration";
    private const string CooldownLevel = "Competitive Cooldown Level";
    private const string Acknowledged = "Acknowledged";

    public OneOf<XpData, ActivityInfoParserError> ParseActivityInfoFromResponse(ActivityInfoResponse response)
    {
        if (response.Html is null)
        {
            return new ActivityInfoParserError
            {
                Message = Messages.ActivityParsers.EmptyHtml
            };
        }

        var document = new HtmlDocument();
        document.LoadHtml(response.Html);

        var rankRaw = document.DocumentNode
            .SelectSingleNode($"//div[contains(text(), '{RankRow}')]")?
            .InnerText.Trim();

        var xpRaw = document.DocumentNode
            .SelectSingleNode($"//div[contains(text(), '{XpRow}')]")?
            .InnerText.Trim();

        int.TryParse(rankRaw.AsSpan()[RankRow.Length..], out var rank);
        int.TryParse(xpRaw.AsSpan()[XpRow.Length..], out var xp);

        return new XpData(rank, xp);
    }

    public OneOf<CooldownData?, ActivityInfoParserError> ParseCooldownInfoFromResponse(ActivityInfoResponse response)
    {
        if (response.Html is null)
        {
            return new ActivityInfoParserError
            {
                Message = Messages.ActivityParsers.EmptyHtml
            };
        }

        var document = new HtmlDocument();
        document.LoadHtml(response.Html);

        var expirationRaw = GetCooldownData(document, CooldownExpirationColumn);
        var level = GetCooldownData(document, CooldownLevel);
        var acknowledged = GetCooldownData(document, Acknowledged);
        var dataExists = DateTimeOffset.TryParse(expirationRaw, out var parsedDate);

        if (!dataExists)
        {
            return new CooldownData(false, null, null, null);
        }

        return new CooldownData(dataExists, parsedDate.ToLocalTime(), Convert.ToInt32(level), acknowledged == "Yes");
    }

    private string? GetCooldownData(HtmlDocument document, string headerText)
    {
        var th = document.DocumentNode.SelectSingleNode($"//th[contains(text(), '{headerText}')]");
        if (th is null)
        {
            return null;
        }

        var allHeaders = th.ParentNode.SelectNodes("th");
        var index = allHeaders.IndexOf(th) + 1;

        return th.ParentNode
            .SelectSingleNode($"following-sibling::tr[1]/td[{index}]")?
            .InnerText
            .Trim();
    }
}
