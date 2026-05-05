using System.Globalization;
using System.Text.Json;

namespace BtcPriceService.Api.Services;

public sealed class BitfinexPriceSourceClient(HttpClient httpClient) : IPriceSourceClient
{
    public string SourceName => "Bitfinex";

    public async Task<double?> GetClosePriceAsync(DateTime hourTimestampUtc, CancellationToken cancellationToken)
    {
        var candleStartUtc = hourTimestampUtc.AddHours(-1);
        var start = new DateTimeOffset(candleStartUtc).ToUnixTimeMilliseconds();
        var end = new DateTimeOffset(hourTimestampUtc).ToUnixTimeMilliseconds();
        var requestUri =
            $"https://api-pub.bitfinex.com/v2/candles/trade:1h:tBTCUSD/hist?start={start}&end={end}&limit=1&sort=1";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array ||
            document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var firstCandle = document.RootElement[0];
        if (firstCandle.ValueKind != JsonValueKind.Array || firstCandle.GetArrayLength() < 3)
        {
            return null;
        }

        var closeElement = firstCandle[2];

        return closeElement.ValueKind switch
        {
            JsonValueKind.Number => closeElement.GetDouble(),
            JsonValueKind.String when double.TryParse(
                closeElement.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var closePrice) => closePrice,
            _ => null
        };
    }
}
