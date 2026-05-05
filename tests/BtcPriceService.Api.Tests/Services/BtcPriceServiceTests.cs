using BtcPriceService.Api.Models;
using BtcPriceService.Api.Persistence;
using BtcPriceService.Api.Persistence.Entities;
using BtcPriceService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using BtcPriceServiceImplementation = BtcPriceService.Api.Services.BtcPriceService;

namespace BtcPriceService.Api.Tests.Services;

public sealed class BtcPriceServiceTests
{
    [Fact]
    public async Task GetOrCreateAggregatedPriceAsync_ReturnsCachedValue_WhenRecordExists()
    {
        await using var dbContext = CreateDbContext();
        var timestamp = new DateTime(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
        dbContext.BtcPriceRecords.Add(new BtcPriceRecord
        {
            Id = Guid.NewGuid(),
            TimestampUtc = timestamp,
            AggregatedPrice = 42000.5,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new BtcPriceServiceImplementation(
            dbContext,
            [new FakeSourceClient("A", 40000.0)],
            new AveragePriceAggregationStrategy());

        var result = await service.GetOrCreateAggregatedPriceAsync(timestamp, CancellationToken.None);

        Assert.True(result.ServedFromCache);
        Assert.Equal(42000.5, result.Price, 8);
        Assert.Equal(0, result.SourcesUsed);
    }

    [Fact]
    public async Task GetOrCreateAggregatedPriceAsync_FetchesAggregatesAndPersists_WhenCacheMiss()
    {
        await using var dbContext = CreateDbContext();
        var timestamp = new DateTime(2024, 01, 01, 12, 0, 0, DateTimeKind.Utc);
        var service = new BtcPriceServiceImplementation(
            dbContext,
            [new FakeSourceClient("A", 50000.0), new FakeSourceClient("B", 51000.0)],
            new AveragePriceAggregationStrategy());

        var result = await service.GetOrCreateAggregatedPriceAsync(timestamp, CancellationToken.None);

        Assert.False(result.ServedFromCache);
        Assert.Equal(50500.0, result.Price, 8);
        Assert.Equal(2, result.SourcesUsed);

        var persisted = await dbContext.BtcPriceRecords.SingleAsync(x => x.TimestampUtc == timestamp);
        Assert.Equal(50500.0, persisted.AggregatedPrice, 8);
    }

    [Fact]
    public async Task GetOrCreateAggregatedPriceAsync_Throws_WhenNoValidSourcePrices()
    {
        await using var dbContext = CreateDbContext();
        var timestamp = new DateTime(2024, 01, 01, 12, 0, 0, DateTimeKind.Utc);
        var service = new BtcPriceServiceImplementation(
            dbContext,
            [new FakeSourceClient("A", null), new FakeSourceClient("B", null)],
            new AveragePriceAggregationStrategy());

        Func<Task> action = async () => await service.GetOrCreateAggregatedPriceAsync(timestamp, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task GetOrCreateAggregatedPriceAsync_Throws_WhenTimestampIsNotHourAccurate()
    {
        await using var dbContext = CreateDbContext();
        var nonHourTimestamp = new DateTime(2024, 01, 01, 12, 15, 0, DateTimeKind.Utc);
        var service = new BtcPriceServiceImplementation(
            dbContext,
            [new FakeSourceClient("A", 50000.0)],
            new AveragePriceAggregationStrategy());

        Func<Task> action = async () => await service.GetOrCreateAggregatedPriceAsync(nonHourTimestamp, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(action);
    }

    [Fact]
    public async Task GetPersistedPricesAsync_ReturnsRangeInclusive_OrderedByTimestamp()
    {
        await using var dbContext = CreateDbContext();
        var t1 = new DateTime(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2024, 01, 01, 11, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2024, 01, 01, 12, 0, 0, DateTimeKind.Utc);

        dbContext.BtcPriceRecords.AddRange(
            MakeRecord(t3, 30000),
            MakeRecord(t1, 10000),
            MakeRecord(t2, 20000));
        await dbContext.SaveChangesAsync();

        var service = new BtcPriceServiceImplementation(
            dbContext,
            [new FakeSourceClient("A", 12345.0)],
            new AveragePriceAggregationStrategy());

        var result = await service.GetPersistedPricesAsync(t1, t2, CancellationToken.None);
        var list = result.ToList();

        Assert.Equal(2, list.Count);
        Assert.Collection(
            list,
            first =>
            {
                Assert.Equal(t1, first.TimestampUtc);
                Assert.Equal(10000, first.Price, 8);
            },
            second =>
            {
                Assert.Equal(t2, second.TimestampUtc);
                Assert.Equal(20000, second.Price, 8);
            });
    }

    [Fact]
    public async Task GetPersistedPricesAsync_Throws_WhenToIsEarlierThanFrom()
    {
        await using var dbContext = CreateDbContext();
        var from = new DateTime(2024, 01, 01, 13, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 01, 01, 12, 0, 0, DateTimeKind.Utc);
        var service = new BtcPriceServiceImplementation(
            dbContext,
            [new FakeSourceClient("A", 1.0)],
            new AveragePriceAggregationStrategy());

        Func<Task> action = async () => await service.GetPersistedPricesAsync(from, to, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(action);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static BtcPriceRecord MakeRecord(DateTime timestamp, double price) =>
        new()
        {
            Id = Guid.NewGuid(),
            TimestampUtc = timestamp,
            AggregatedPrice = price,
            CreatedAtUtc = DateTime.UtcNow
        };

    private sealed class FakeSourceClient(string sourceName, double? price) : IPriceSourceClient
    {
        public string SourceName { get; } = sourceName;

        public Task<double?> GetClosePriceAsync(DateTime hourTimestampUtc, CancellationToken cancellationToken) =>
            Task.FromResult(price);
    }
}
