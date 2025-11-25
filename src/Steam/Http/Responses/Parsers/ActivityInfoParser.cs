using OneOf;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using XpGetter.Dto;
using XpGetter.Errors;

namespace XpGetter.Steam.Http.Responses.Parsers;

public class ActivityInfoParser
{
    public OneOf<XpData, ActivityInfoParserError> ParseActivityInfoFromHtml(HtmlDocument document)
    {
        var tables = document.DocumentNode
            .QuerySelectorAll("table.generic_kv_table");

        if (tables is null)
        {
            return new ActivityInfoParserError { Message = "Table with class 'generic_kv_table' not found." };
        }

        var lines = tables
            .Select(x => x.QuerySelectorAll(".generic_kv_line").Select(n => n.InnerText.Trim()))
            .SelectMany(x => x);

        var rank = 0;
        var xp = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("CS:GO Profile Rank:"))
            {
                var value = line["CS:GO Profile Rank:".Length..].Trim();
                int.TryParse(value, out rank);
            }
            else if (line.StartsWith("Experience points earned towards next rank:"))
            {
                var value = line["Experience points earned towards next rank:".Length..].Trim();
                int.TryParse(value, out xp);
            }
        }

        return new XpData(rank, xp);
    }
}
