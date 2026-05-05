using BtcPriceService.Api.Models;
using BtcPriceService.Api.Persistence;
using BtcPriceService.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BtcPriceService.Api.Services;

public sealed class BtcPriceService(
    AppDbContext dbContext,
    IEnumerable<IPriceSourceClient> sourceClients,
    IPriceAggregationStrategy aggregationStrategy) : IBtcPriceService
{
    public async Task<AggregatedPriceResponse> GetOrCreateAggregatedPriceAsync(
        DateTime requestedTimestampUtc,
        CancellationToken cancellationToken)
    {
        var hourTimestampUtc = NormalizeAndValidateHourUtc(requestedTimestampUtc);

        var cachedRecord = await dbContext.BtcPriceRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TimestampUtc == hourTimestampUtc, cancellationToken);

        if (cachedRecord is not null)
        {
            return new AggregatedPriceResponse(
                cachedRecord.TimestampUtc,
                cachedRecord.AggregatedPrice,
                true,
                0);
        }

        var prices = new List<double>();
        foreach (var sourceClient in sourceClients)
        {
            var closePrice = await sourceClient.GetClosePriceAsync(hourTimestampUtc, cancellationToken);
            if (closePrice.HasValue)
            {
                prices.Add(closePrice.Value);
            }
        }

        if (prices.Count == 0)
        {
            throw new InvalidOperationException("No source returned a valid BTC/USD close price.");
        }

        var aggregatedPrice = aggregationStrategy.Aggregate(prices);
        var record = new BtcPriceRecord
        {
            Id = Guid.NewGuid(),
            TimestampUtc = hourTimestampUtc,
            AggregatedPrice = aggregatedPrice,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.BtcPriceRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AggregatedPriceResponse(
            record.TimestampUtc,
            record.AggregatedPrice,
            false,
            prices.Count);
    }

    public async Task<IReadOnlyCollection<PersistedPriceResponse>> GetPersistedPricesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var normalizedFrom = NormalizeAndValidateHourUtc(fromUtc);
        var normalizedTo = NormalizeAndValidateHourUtc(toUtc);

        if (normalizedTo < normalizedFrom)
        {
            throw new ArgumentException("'to' must be greater than or equal to 'from'.");
        }

        return await dbContext.BtcPriceRecords
            .AsNoTracking()
            .Where(x => x.TimestampUtc >= normalizedFrom && x.TimestampUtc <= normalizedTo)
            .OrderBy(x => x.TimestampUtc)
            .Select(x => new PersistedPriceResponse(x.TimestampUtc, x.AggregatedPrice))
            .ToListAsync(cancellationToken);
    }

    private static DateTime NormalizeAndValidateHourUtc(DateTime timestampUtc)
    {
        var utc = timestampUtc.Kind == DateTimeKind.Utc
            ? timestampUtc
            : timestampUtc.ToUniversalTime();

        if (utc.Minute != 0 || utc.Second != 0 || utc.Millisecond != 0)
        {
            throw new ArgumentException("Timestamp must have hour accuracy (minutes/seconds/milliseconds must be zero).");
        }

        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
    }
}
