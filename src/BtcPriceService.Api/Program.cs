using BtcPriceService.Api.Persistence;
using BtcPriceService.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=btc-prices.db";

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddHttpClient<BitstampPriceSourceClient>();
builder.Services.AddHttpClient<BitfinexPriceSourceClient>();

builder.Services.AddScoped<IPriceSourceClient, BitstampPriceSourceClient>();
builder.Services.AddScoped<IPriceSourceClient, BitfinexPriceSourceClient>();
builder.Services.AddScoped<IPriceAggregationStrategy, AveragePriceAggregationStrategy>();
builder.Services.AddScoped<IBtcPriceService, global::BtcPriceService.Api.Services.BtcPriceService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
