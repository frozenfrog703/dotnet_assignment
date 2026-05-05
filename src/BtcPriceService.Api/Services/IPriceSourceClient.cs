namespace BtcPriceService.Api.Services;

public interface IPriceSourceClient
{
    string SourceName { get; }
    Task<double?> GetClosePriceAsync(DateTime hourTimestampUtc, CancellationToken cancellationToken);
}
