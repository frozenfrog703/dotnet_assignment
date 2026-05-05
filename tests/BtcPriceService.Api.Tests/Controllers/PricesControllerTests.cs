using BtcPriceService.Api.Controllers;
using BtcPriceService.Api.Models;
using BtcPriceService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BtcPriceService.Api.Tests.Controllers;

public sealed class PricesControllerTests
{
    [Fact]
    public async Task GetAggregatedPrice_ReturnsOk_WhenServiceSucceeds()
    {
        var timestamp = new DateTime(2024, 01, 01, 10, 0, 0, DateTimeKind.Utc);
        var service = new FakeBtcPriceService(
            getOrCreate: (_, _) => Task.FromResult(new AggregatedPriceResponse(timestamp, 42000.0, true, 0)),
            getRange: (_, _, _) => Task.FromResult<IReadOnlyCollection<PersistedPriceResponse>>([]));
        var controller = new PricesController(service);

        var result = await controller.GetAggregatedPrice(timestamp, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AggregatedPriceResponse>(ok.Value);
        Assert.Equal(42000.0, payload.Price, 8);
    }

    [Fact]
    public async Task GetAggregatedPrice_ReturnsBadRequest_WhenServiceThrowsArgumentException()
    {
        var service = new FakeBtcPriceService(
            getOrCreate: (_, _) => throw new ArgumentException("invalid timestamp"),
            getRange: (_, _, _) => Task.FromResult<IReadOnlyCollection<PersistedPriceResponse>>([]));
        var controller = new PricesController(service);

        var result = await controller.GetAggregatedPrice(DateTime.UtcNow, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAggregatedPrice_Returns503_WhenServiceThrowsInvalidOperationException()
    {
        var service = new FakeBtcPriceService(
            getOrCreate: (_, _) => throw new InvalidOperationException("sources unavailable"),
            getRange: (_, _, _) => Task.FromResult<IReadOnlyCollection<PersistedPriceResponse>>([]));
        var controller = new PricesController(service);

        var result = await controller.GetAggregatedPrice(DateTime.UtcNow, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetPersistedPrices_ReturnsOk_WhenServiceSucceeds()
    {
        var from = new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 01, 01, 2, 0, 0, DateTimeKind.Utc);
        var service = new FakeBtcPriceService(
            getOrCreate: (_, _) => Task.FromResult(new AggregatedPriceResponse(from, 1.0, true, 0)),
            getRange: (_, _, _) => Task.FromResult<IReadOnlyCollection<PersistedPriceResponse>>(
            [
                new PersistedPriceResponse(from, 10000),
                new PersistedPriceResponse(to, 20000)
            ]));
        var controller = new PricesController(service);

        var result = await controller.GetPersistedPrices(from, to, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IReadOnlyCollection<PersistedPriceResponse>>(ok.Value);
        Assert.Equal(2, payload.Count);
    }

    [Fact]
    public async Task GetPersistedPrices_ReturnsBadRequest_WhenServiceThrowsArgumentException()
    {
        var service = new FakeBtcPriceService(
            getOrCreate: (_, _) => Task.FromResult(new AggregatedPriceResponse(DateTime.UtcNow, 1.0, true, 0)),
            getRange: (_, _, _) => throw new ArgumentException("invalid range"));
        var controller = new PricesController(service);

        var result = await controller.GetPersistedPrices(DateTime.UtcNow, DateTime.UtcNow, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class FakeBtcPriceService(
        Func<DateTime, CancellationToken, Task<AggregatedPriceResponse>> getOrCreate,
        Func<DateTime, DateTime, CancellationToken, Task<IReadOnlyCollection<PersistedPriceResponse>>> getRange)
        : IBtcPriceService
    {
        public Task<AggregatedPriceResponse> GetOrCreateAggregatedPriceAsync(
            DateTime requestedTimestampUtc,
            CancellationToken cancellationToken) => getOrCreate(requestedTimestampUtc, cancellationToken);

        public Task<IReadOnlyCollection<PersistedPriceResponse>> GetPersistedPricesAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken) => getRange(fromUtc, toUtc, cancellationToken);
    }
}
