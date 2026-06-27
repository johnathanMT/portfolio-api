using Microsoft.AspNetCore.Mvc;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Persistent visitor analytics for the 3D "Visitor Globe" — CLEAN 3-LAYER:
///   Controller (HTTP) → IVisitorService (normalize/orchestrate) → IVisitorRepository (raw SQL).
///
///   GET  /api/visitors             → { success, totalVisits }
///   POST /api/visitors/hit?country=Japan → increments total (+ per-country) → { success, totalVisits }
///   GET  /api/visitors/countries   → { success, countries: [{ country, visits }] }
///
/// The global per-IP rate limiter (200/min) protects these endpoints.
/// </summary>
[ApiController]
[Route("api/visitors")]
public class VisitorsController : ControllerBase
{
    private readonly IVisitorService _service;
    private readonly ILogger<VisitorsController> _logger;

    public VisitorsController(IVisitorService service, ILogger<VisitorsController> logger)
    {
        _service = service; _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try { return Ok(new { success = true, totalVisits = await _service.GetTotalAsync() }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read visitor count.");
            return StatusCode(503, new { success = false, message = "Visitor counter unavailable." });
        }
    }

    [HttpPost("hit")]
    public async Task<IActionResult> Hit([FromQuery] string? country)
    {
        try { return Ok(new { success = true, totalVisits = await _service.HitAsync(country) }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment visitor count.");
            return StatusCode(503, new { success = false, message = "Visitor counter unavailable." });
        }
    }

    [HttpGet("countries")]
    public async Task<IActionResult> Countries()
    {
        try
        {
            var rows = await _service.GetCountriesAsync();
            var countries = rows.Select(c => new { country = c.Country, visits = c.Visits });
            return Ok(new { success = true, countries });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read visitor breakdown by country.");
            return StatusCode(503, new { success = false, message = "Visitor breakdown unavailable." });
        }
    }
}
