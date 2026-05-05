namespace BtcPriceService.Api.Models;

public sealed record AggregatedPriceResponse(
    DateTime TimestampUtc,
    double Price,
    bool ServedFromCache,
    int SourcesUsed
);
