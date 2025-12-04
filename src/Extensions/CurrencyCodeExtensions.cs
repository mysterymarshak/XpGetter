using System.Globalization;
using SteamKit2;

namespace XpGetter.Extensions;

public static class CurrencyCodeExtensions
{
    private static readonly Dictionary<ECurrencyCode, CultureInfo> CultureMap = new()
        {
            { ECurrencyCode.USD, new CultureInfo("en-US") },
            { ECurrencyCode.GBP, new CultureInfo("en-GB") },
            { ECurrencyCode.EUR, new CultureInfo("fr-FR") },
            { ECurrencyCode.CHF, new CultureInfo("de-CH") },
            { ECurrencyCode.RUB, new CultureInfo("ru-RU") },
            { ECurrencyCode.PLN, new CultureInfo("pl-PL") },
            { ECurrencyCode.BRL, new CultureInfo("pt-BR") },
            { ECurrencyCode.JPY, new CultureInfo("ja-JP") },
            { ECurrencyCode.NOK, new CultureInfo("nb-NO") },
            { ECurrencyCode.IDR, new CultureInfo("id-ID") },
            { ECurrencyCode.MYR, new CultureInfo("ms-MY") },
            { ECurrencyCode.PHP, new CultureInfo("fil-PH") },
            { ECurrencyCode.SGD, new CultureInfo("en-SG") },
            { ECurrencyCode.THB, new CultureInfo("th-TH") },
            { ECurrencyCode.VND, new CultureInfo("vi-VN") },
            { ECurrencyCode.KRW, new CultureInfo("ko-KR") },
            { ECurrencyCode.TRY, new CultureInfo("tr-TR") },
            { ECurrencyCode.UAH, new CultureInfo("uk-UA") },
            { ECurrencyCode.MXN, new CultureInfo("es-MX") },
            { ECurrencyCode.CAD, new CultureInfo("en-CA") },
            { ECurrencyCode.AUD, new CultureInfo("en-AU") },
            { ECurrencyCode.NZD, new CultureInfo("en-NZ") },
            { ECurrencyCode.CNY, new CultureInfo("zh-CN") },
            { ECurrencyCode.INR, new CultureInfo("en-IN") },
            { ECurrencyCode.CLP, new CultureInfo("es-CL") },
            { ECurrencyCode.PEN, new CultureInfo("es-PE") },
            { ECurrencyCode.COP, new CultureInfo("es-CO") },
            { ECurrencyCode.ZAR, new CultureInfo("en-ZA") },
            { ECurrencyCode.HKD, new CultureInfo("zh-HK") },
            { ECurrencyCode.TWD, new CultureInfo("zh-TW") },
            { ECurrencyCode.SAR, new CultureInfo("ar-SA") },
            { ECurrencyCode.AED, new CultureInfo("ar-AE") },
            { ECurrencyCode.ARS, new CultureInfo("es-AR") },
            { ECurrencyCode.ILS, new CultureInfo("he-IL") },
            { ECurrencyCode.BYN, new CultureInfo("be-BY") },
            { ECurrencyCode.KZT, new CultureInfo("kk-KZ") },
            { ECurrencyCode.KWD, new CultureInfo("ar-KW") },
            { ECurrencyCode.QAR, new CultureInfo("ar-QA") },
            { ECurrencyCode.CRC, new CultureInfo("es-CR") },
            { ECurrencyCode.UYU, new CultureInfo("es-UY") },
            { ECurrencyCode.BGN, new CultureInfo("bg-BG") },
            { ECurrencyCode.HRK, new CultureInfo("hr-HR") },
            { ECurrencyCode.CZK, new CultureInfo("cs-CZ") },
            { ECurrencyCode.DKK, new CultureInfo("da-DK") },
            { ECurrencyCode.HUF, new CultureInfo("hu-HU") },
            { ECurrencyCode.RON, new CultureInfo("ro-RO") }
        };

    public static string FormatValue(this ECurrencyCode currency, double value)
    {
        if (currency == ECurrencyCode.Invalid)
        {
            return value.ToString("N2", CultureInfo.InvariantCulture);
        }

        if (!CultureMap.TryGetValue(currency, out var culture))
        {
            culture = CultureInfo.InvariantCulture;
        }

        return value.ToString("C", culture);
    }
}
