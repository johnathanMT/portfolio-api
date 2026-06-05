using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;

namespace PortfolioApi.Controllers;

/// <summary>
/// Health check endpoint used by Render and Docker HEALTHCHECK.
/// GET /health → 200 OK when the API and DB are reachable.
/// </summary>
[ApiController]
[Route("health")]
[ApiExplorerSettings(IgnoreApi = true)] // Hidden from Swagger
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext db, ILogger<HealthController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            // Ping the database
            await _db.Database.ExecuteSqlRawAsync("SELECT 1");

            return Ok(new
            {
                status    = "healthy",
                timestamp = DateTime.UtcNow,
                database  = "connected",
                version   = "1.0.0",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed — database unreachable.");

            return StatusCode(503, new
            {
                status    = "unhealthy",
                timestamp = DateTime.UtcNow,
                database  = "disconnected",
                error     = ex.Message,
            });
        }
    }
}
