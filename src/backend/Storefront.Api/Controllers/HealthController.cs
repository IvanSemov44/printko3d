using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Api.Errors;
using Storefront.Application.Health;

namespace Storefront.Api.Controllers;

/// <summary>Is the API up, and can it reach its database. Used by Docker and by the hosting platform.</summary>
[ApiController]
[AllowAnonymous]
public sealed class HealthController(HealthService healthService) : ControllerBase
{
    /// <summary>Answers 200 when the database is reachable.</summary>
    /// <remarks>Errors: 503 health.database_unreachable.</remarks>
    [HttpGet("/health")]
    [ProducesResponseType<HealthStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatus>> GetHealth(CancellationToken cancellationToken)
    {
        var status = await healthService.CheckAsync(cancellationToken);
        if (status.IsHealthy)
        {
            return Ok(status);
        }

        var problem = ApiProblems.Create(
            StatusCodes.Status503ServiceUnavailable,
            "Service unavailable",
            "The database is not reachable.",
            "health.database_unreachable");
        return StatusCode(StatusCodes.Status503ServiceUnavailable, problem);
    }
}
