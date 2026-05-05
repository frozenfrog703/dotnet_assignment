namespace BtcPriceService.Api.Persistence.Entities;

public sealed class BtcPriceRecord
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public double AggregatedPrice { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
