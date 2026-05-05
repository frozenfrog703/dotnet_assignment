namespace BtcPriceService.Api.Models;

public sealed record PersistedPriceResponse(
    DateTime TimestampUtc,
    double Price
);
