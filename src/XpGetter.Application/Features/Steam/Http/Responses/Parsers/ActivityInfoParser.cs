using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using OneOf;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;

namespace XpGetter.Application.Features.Steam.Http.Responses.Parsers;

public class ActivityInfoParser
{
    private const string RankRow = "CS:GO Profile Rank:";
    private const string XpRow = "Experience points earned towards next rank:";

    public OneOf<XpData, ActivityInfoParserError> ParseActivityInfoFromHtml(HtmlDocument document)
    {
        var tables = document.DocumentNode
            .QuerySelectorAll("table.generic_kv_table");

        if (tables is null)
        {
            return new ActivityInfoParserError { Message = Messages.ActivityParsers.Activity.NoDataTables };
        }

        var lines = tables
            .Select(x => x.QuerySelectorAll(".generic_kv_line").Select(n => n.InnerText.Trim()))
            .SelectMany(x => x);

        var rank = 0;
        var xp = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith(RankRow))
            {
                var value = line.AsSpan()[RankRow.Length..];
                int.TryParse(value, out rank);
            }
            else if (line.StartsWith(XpRow))
            {
                var value = line.AsSpan()[XpRow.Length..];
                int.TryParse(value, out xp);
            }
        }

        return new XpData(rank, xp);
    }
}