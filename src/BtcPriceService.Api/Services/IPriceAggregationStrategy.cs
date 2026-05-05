namespace BtcPriceService.Api.Services;

public interface IPriceAggregationStrategy
{
    double Aggregate(IReadOnlyCollection<double> prices);
}
