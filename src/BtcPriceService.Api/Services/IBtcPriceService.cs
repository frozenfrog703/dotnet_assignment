using BtcPriceService.Api.Models;

namespace BtcPriceService.Api.Services;

public interface IBtcPriceService
{
    Task<AggregatedPriceResponse> GetOrCreateAggregatedPriceAsync(DateTime requestedTimestampUtc, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PersistedPriceResponse>> GetPersistedPricesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
