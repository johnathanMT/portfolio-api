using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;

namespace PortfolioApi.Controllers;

/// <summary>
/// Persistent visitor analytics for the 3D "Visitor Globe" section.
///   GET  /api/visitors            → { success, totalVisits }
///   POST /api/visitors/hit?country=Japan → increments total (+ per-country) →
///                                   { success, totalVisits }
///   GET  /api/visitors/countries  → { success, countries: [{ country, visits }] }
///
/// Storage is created on first use with raw SQL — no EF migration required:
///   • visitor_stats     : single row (id=1) holding the global total.
///   • visitor_countries : one row per country (country PK + running count).
/// A dedicated per-country table is cleaner than adding a column to the
/// single-row total table, and needs NO destructive ALTER/DROP of existing data.
/// All writes are atomic/parameterized, so they are concurrency- and
/// injection-safe. The global per-IP rate limiter (200/min) protects these.
/// </summary>
[ApiController]
[Route("api/visitors")]
public class VisitorsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<VisitorsController> _logger;

    private static bool _tablesEnsured;
    private static readonly SemaphoreSlim _ensureLock = new(1, 1);

    public VisitorsController(AppDbContext db, ILogger<VisitorsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task EnsureTablesAsync()
    {
        if (_tablesEnsured) return;
        await _ensureLock.WaitAsync();
        try
        {
            if (_tablesEnsured) return;

            // Global total (single row).
            await _db.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS visitor_stats (
                      id            INT PRIMARY KEY,
                      total_visits  BIGINT   NOT NULL DEFAULT 0,
                      updated_at    DATETIME NOT NULL
                  );");
            await _db.Database.ExecuteSqlRawAsync(
                "INSERT IGNORE INTO visitor_stats (id, total_visits, updated_at) VALUES (1, 0, UTC_TIMESTAMP());");

            // Per-country breakdown (country is the primary key).
            await _db.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS visitor_countries (
                      country     VARCHAR(100) NOT NULL PRIMARY KEY,
                      visits      BIGINT       NOT NULL DEFAULT 0,
                      updated_at  DATETIME     NOT NULL
                  );");

            _tablesEnsured = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    private async Task<long> ReadTotalAsync()
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT total_visits FROM visitor_stats WHERE id = 1;";
        var r = await cmd.ExecuteScalarAsync();
        return r is null or DBNull ? 0L : Convert.ToInt64(r);
    }

    // GET /api/visitors → current total (does NOT increment)
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            await EnsureTablesAsync();
            return Ok(new { success = true, totalVisits = await ReadTotalAsync() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read visitor count.");
            return StatusCode(503, new { success = false, message = "Visitor counter unavailable." });
        }
    }

    // POST /api/visitors/hit?country=Japan → increment total (+ that country)
    [HttpPost("hit")]
    public async Task<IActionResult> Hit([FromQuery] string? country)
    {
        try
        {
            await EnsureTablesAsync();

            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE visitor_stats SET total_visits = total_visits + 1, updated_at = UTC_TIMESTAMP() WHERE id = 1;");

            var name = (country ?? string.Empty).Trim();
            if (name.Length > 100) name = name[..100];
            if (name.Length > 0)
            {
                // Parameterized upsert ({0} → bound parameter, injection-safe).
                await _db.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO visitor_countries (country, visits, updated_at)
                      VALUES ({0}, 1, UTC_TIMESTAMP())
                      ON DUPLICATE KEY UPDATE visits = visits + 1, updated_at = UTC_TIMESTAMP();",
                    name);
            }

            return Ok(new { success = true, totalVisits = await ReadTotalAsync() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment visitor count.");
            return StatusCode(503, new { success = false, message = "Visitor counter unavailable." });
        }
    }

    // GET /api/visitors/countries → grouped count, highest first
    [HttpGet("countries")]
    public async Task<IActionResult> Countries()
    {
        try
        {
            await EnsureTablesAsync();

            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT country, visits FROM visitor_countries ORDER BY visits DESC, country ASC LIMIT 100;";

            var countries = new List<object>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    countries.Add(new
                    {
                        country = Convert.ToString(reader.GetValue(0)) ?? string.Empty,
                        visits = Convert.ToInt64(reader.GetValue(1)),
                    });
                }
            }

            return Ok(new { success = true, countries });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read visitor breakdown by country.");
            return StatusCode(503, new { success = false, message = "Visitor breakdown unavailable." });
        }
    }
}
