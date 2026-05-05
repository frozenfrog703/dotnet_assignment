using System.Globalization;
using System.Text.Json;

namespace BtcPriceService.Api.Services;

public sealed class BitstampPriceSourceClient(HttpClient httpClient) : IPriceSourceClient
{
    public string SourceName => "Bitstamp";

    public async Task<double?> GetClosePriceAsync(DateTime hourTimestampUtc, CancellationToken cancellationToken)
    {
        var candleStartUtc = hourTimestampUtc.AddHours(-1);
        var unixSeconds = new DateTimeOffset(candleStartUtc).ToUnixTimeSeconds();
        var requestUri = $"https://www.bitstamp.net/api/v2/ohlc/btcusd/?step=3600&limit=1&start={unixSeconds}";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var dataElement) ||
            !dataElement.TryGetProperty("ohlc", out var candlesElement) ||
            candlesElement.ValueKind != JsonValueKind.Array ||
            candlesElement.GetArrayLength() == 0)
        {
            return null;
        }

        var firstCandle = candlesElement[0];
        if (!firstCandle.TryGetProperty("close", out var closeElement))
        {
            return null;
        }

        return double.TryParse(closeElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var closePrice)
            ? closePrice
            : null;
    }
}
