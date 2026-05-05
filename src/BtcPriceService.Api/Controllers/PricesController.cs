using BtcPriceService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BtcPriceService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PricesController(IBtcPriceService btcPriceService) : ControllerBase
{
    [HttpGet("aggregate")]
    public async Task<IActionResult> GetAggregatedPrice([FromQuery] DateTime timestamp, CancellationToken cancellationToken)
    {
        try
        {
            var result = await btcPriceService.GetOrCreateAggregatedPriceAsync(timestamp, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = exception.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetPersistedPrices(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await btcPriceService.GetPersistedPricesAsync(from, to, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
