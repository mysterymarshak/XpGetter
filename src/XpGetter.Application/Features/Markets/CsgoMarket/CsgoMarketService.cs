using System.Text;
using Newtonsoft.Json;
using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.ExchangeRates;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Markets.CsgoMarket;

public class CsgoMarketService : IMarketService
{
    private static readonly IReadOnlySet<ECurrencyCode> UnsupportedCurrencies = new HashSet<ECurrencyCode>
    {
        ECurrencyCode.GBP, ECurrencyCode.CHF, ECurrencyCode.PLN, ECurrencyCode.JPY,
        ECurrencyCode.NOK, ECurrencyCode.SGD, ECurrencyCode.THB, ECurrencyCode.VND,
        ECurrencyCode.KRW, ECurrencyCode.TRY, ECurrencyCode.MXN, ECurrencyCode.CAD,
        ECurrencyCode.AUD, ECurrencyCode.NZD, ECurrencyCode.INR, ECurrencyCode.CLP,
        ECurrencyCode.PEN, ECurrencyCode.ZAR, ECurrencyCode.HKD, ECurrencyCode.TWD,
        ECurrencyCode.SAR, ECurrencyCode.AED, ECurrencyCode.ARS, ECurrencyCode.ILS,
        ECurrencyCode.BYN, ECurrencyCode.KWD, ECurrencyCode.QAR, ECurrencyCode.CRC,
        ECurrencyCode.UYU, ECurrencyCode.BGN, ECurrencyCode.HRK, ECurrencyCode.CZK,
        ECurrencyCode.DKK, ECurrencyCode.HUF, ECurrencyCode.RON
    };

    private const string BaseUrl = "https://api.marketapp.online/core/item-price/v2";
    private const ECurrencyCode DefaultCurrency = ECurrencyCode.USD;

    private readonly HttpClient _client;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger _logger;

    public CsgoMarketService(HttpClient httpClient, IExchangeRateService exchangeRateService, ILogger logger)
    {
        _client = httpClient;
        _exchangeRateService = exchangeRateService;
        _client.BaseAddress = new Uri(BaseUrl);
        _logger = logger;
    }

    public async Task<IEnumerable<PriceDto>> GetItemsPriceAsync(IReadOnlyList<string> names,
        ECurrencyCode currency, SteamSession session, IProgressContext ctx)
    {
        if (names.Count == 0)
        {
            return [];
        }

        IProgressTask? getExchangeRateTask = null;
        var tasks = new List<Task>(2);

        var originalCurrency = currency;
        var needConversion = false;

        if (UnsupportedCurrencies.Contains(currency))
        {
            _logger.Warning(Messages.Market.UnsupportedCurrency, currency, DefaultCurrency);
            currency = DefaultCurrency;
            needConversion = true;

            getExchangeRateTask = ctx.AddTask(session, Messages.Statuses.RetrievingExchangeRate);
            var exchangeTask = _exchangeRateService.GetExchangeRateAsync(currency, originalCurrency,
                                                                         session, getExchangeRateTask);
            tasks.Add(exchangeTask);
        }

        var requestTaskIndex = tasks.Count;
        tasks.Add(GetPricesAsync(names, currency));

        await Task.WhenAll(tasks);

        var pricesResult = ((Task<List<ItemPriceResponse>>)tasks[requestTaskIndex]).Result;
        if (pricesResult.Count == 0)
        {
            return [];
        }

        if (needConversion)
        {
            var exchangeRate = ((Task<ExchangeRateDto?>)tasks[0]).Result;
            PatchPrices(exchangeRate, pricesResult, ref currency, ref originalCurrency, session, getExchangeRateTask!);
        }

        return pricesResult
            .Select(x => x.Prices.Select(y => new PriceDto(x.ItemName, y.Value, y.Provider, currency)))
            .SelectMany(x => x)
            .ToList();
    }

    private async Task<List<ItemPriceResponse>> GetPricesAsync(IReadOnlyList<string> names, ECurrencyCode currency)
    {
        try
        {
            var payload = new ItemPriceRequest { ItemNames = names };
            var message = new HttpRequestMessage(HttpMethod.Post, $"?currency={currency}");
            message.Content = new StringContent(JsonConvert.SerializeObject(payload, Formatting.None),
                                                Encoding.UTF8, "application/json");

            var response = await _client.SendAsync(message);
            response.EnsureSuccessStatusCode();

            var contentAsString = await response.Content.ReadAsStringAsync();

            var deserialized = JsonConvert.DeserializeObject<List<ItemPriceResponse>>(contentAsString);
            if (deserialized is null)
            {
                var error = new MarketServiceError
                {
                    Message = string.Format(Messages.Market.DeserializationError, contentAsString)
                };

                _logger.LogError(error);
                return [];
            }

            return deserialized;
        }
        catch (Exception exception)
        {
            var error = new MarketServiceError
            {
                Message = string.Format(Messages.Market.GetPriceException, string.Join(", ", names)),
                Exception = exception
            };

            _logger.LogError(error);
        }

        return [];
    }

    private void PatchPrices(ExchangeRateDto? exchangeRate, List<ItemPriceResponse> pricesResult,
                             ref ECurrencyCode currency, ref ECurrencyCode originalCurrency,
                             SteamSession session, IProgressTask task)
    {
        if (exchangeRate is not null)
        {
            _logger.Debug(Messages.Market.SuccessfullyConverted, currency, originalCurrency, exchangeRate);
            task.SetResult(session, Messages.Statuses.RetrievingExchangeRateOk);
            currency = originalCurrency;

            foreach (var item in pricesResult)
            {
                foreach (var itemPrice in item.Prices)
                {
                    itemPrice.Value *= exchangeRate.Value;
                }
            }
        }
        else
        {
            _logger.Warning(Messages.Market.FailedToGetExchangeRate, currency,
                            originalCurrency, DefaultCurrency);
            task.SetResult(session, Messages.Statuses.RetrievingExchangeRateError);
        }
    }
}
