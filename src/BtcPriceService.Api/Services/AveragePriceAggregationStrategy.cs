namespace BtcPriceService.Api.Services;

public sealed class AveragePriceAggregationStrategy : IPriceAggregationStrategy
{
    public double Aggregate(IReadOnlyCollection<double> prices)
    {
        if (prices.Count == 0)
        {
            throw new InvalidOperationException("At least one source price is required for aggregation.");
        }

        return prices.Average();
    }
}
