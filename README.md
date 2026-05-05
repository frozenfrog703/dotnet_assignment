# BTC Price Aggregation Microservice (.NET)

This project implements a RESTful microservice that retrieves BTC/USD close prices from multiple external providers, aggregates them, caches the result in a datastore, and serves persisted data for range queries.

## Tech Stack

- .NET 8 Web API
- Entity Framework Core
- SQLite datastore
- External providers:
  - Bitstamp OHLC API
  - Bitfinex Candles API

## Implemented Requirements

- Endpoint to request aggregated BTC price at a specific hour time-point
  - If the timestamp already exists in the datastore, data is returned immediately from cache
  - Otherwise data is fetched from sources, aggregated (average), persisted, and returned
- Endpoint to fetch persisted BTC prices for a user-specified time range
- RESTful API interface
- Floating-point price handling (`double`)
- Hour-accuracy validation (minutes/seconds/milliseconds must be zero)
- Extensible architecture for adding new providers and changing aggregation strategy

## Project Structure

- `src/BtcPriceService.Api/Program.cs` - app composition and DI setup
- `src/BtcPriceService.Api/Controllers/PricesController.cs` - REST endpoints
- `src/BtcPriceService.Api/Services/` - source clients, aggregation, orchestration service
- `src/BtcPriceService.Api/Persistence/` - EF Core context and entities

## API Endpoints

### 1) Get aggregated price for a specific hour

`GET /api/prices/aggregate?timestamp=2024-01-01T10:00:00Z`

Response:

```json
{
  "timestampUtc": "2024-01-01T10:00:00Z",
  "price": 42123.45,
  "servedFromCache": false,
  "sourcesUsed": 2
}
```

### 2) Get persisted prices in a time range

`GET /api/prices?from=2024-01-01T00:00:00Z&to=2024-01-02T00:00:00Z`

Response:

```json
[
  {
    "timestampUtc": "2024-01-01T00:00:00Z",
    "price": 43000.12
  }
]
```

## Validation Rules

- `timestamp`, `from`, and `to` must have hour accuracy:
  - Minutes = 0
  - Seconds = 0
  - Milliseconds = 0
- A requested timestamp is treated as the candle close time (for example, `10:00:00Z` uses the `09:00:00Z-10:00:00Z` candle)
- `to` must be greater than or equal to `from`

## How to Run

Prerequisites:

- .NET SDK 8.0+ installed

Commands:

```bash
cd src/BtcPriceService.Api
dotnet restore
dotnet run
```

By default, the API uses:

- SQLite file: `btc-prices.db`
- Swagger UI (development): `https://localhost:<port>/swagger`

## Notes on Extensibility

- To add a new market data source, implement `IPriceSourceClient` and register it in `Program.cs`
- To change aggregation logic, implement `IPriceAggregationStrategy` and replace registration in `Program.cs`

## Automated Tests

The repository includes an `xUnit` test project:

- `tests/BtcPriceService.Api.Tests/`

### Run tests (from repository root)

1) Restore dependencies:

```bash
dotnet restore
```

2) Run all tests:

```bash
dotnet test dotnet_assignment.sln
```

You should see a result similar to:

```text
Passed!  - Failed: 0, Passed: 11, Skipped: 0
```

3) (Optional) Run tests with coverage collection:

```bash
dotnet test dotnet_assignment.sln --collect:"XPlat Code Coverage"
```

### Run only one test project

```bash
dotnet test tests/BtcPriceService.Api.Tests/BtcPriceService.Api.Tests.csproj
```

Current test coverage includes:

- Cache hit vs cache miss behavior for aggregation endpoint service logic
- Aggregation and persistence behavior
- Input validation (hour accuracy and range correctness)
- Controller status-code mapping (`200`, `400`, `503`)

## Manual End-to-End Verification Checklist

After `dotnet run`, use Swagger or curl/Postman:

1. Request a new hour:
   - `GET /api/prices/aggregate?timestamp=2024-01-01T10:00:00Z`
   - Expect `servedFromCache = false`
2. Request the same hour again:
   - Expect `servedFromCache = true` and same `price`
3. Request a range that includes that hour:
   - `GET /api/prices?from=2024-01-01T09:00:00Z&to=2024-01-01T10:00:00Z`
   - Expect item for `2024-01-01T10:00:00Z`
4. Validate bad timestamp:
   - `GET /api/prices/aggregate?timestamp=2024-01-01T10:30:00Z`
   - Expect `400 Bad Request`
5. Validate bad range:
   - `GET /api/prices?from=2024-01-01T12:00:00Z&to=2024-01-01T11:00:00Z`
   - Expect `400 Bad Request`
