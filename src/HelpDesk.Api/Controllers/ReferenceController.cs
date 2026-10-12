using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

/// <summary>Read-only data for drop-downs and the dashboard.</summary>
[ApiController]
[Route("api")]
public class ReferenceController : ControllerBase
{
    private readonly IIncidentRepository _repository;

    public ReferenceController(IIncidentRepository repository) => _repository = repository;

    [HttpGet("lookups")]
    public async Task<ActionResult<Lookups>> GetLookups(CancellationToken ct) =>
        Ok(await _repository.GetLookupsAsync(ct));

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStats>> GetDashboard(CancellationToken ct) =>
        Ok(await _repository.GetDashboardStatsAsync(ct));

    /// <summary>Simple health check for monitoring / after a deployment.</summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "OK", timeUtc = DateTime.UtcNow });
}
