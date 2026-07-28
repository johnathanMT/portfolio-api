using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortfolioApi.Common;
using PortfolioApi.Data;
using PortfolioApi.DTOs.Astrology;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;
using PortfolioApi.Security;

namespace PortfolioApi.Controllers;

/// <summary>
/// Vedic astrology — computes a sidereal Rasi (D1) birth chart from birth details.
/// Public, stateless, rate-limited. POST /api/astrology/chart.
/// </summary>
[ApiController]
[Route("api/astrology")]
[Produces("application/json")]
public class AstrologyController : ControllerBase
{
    private readonly IAstrologyService _service;
    private readonly AppDbContext _db;
    private readonly string _encKey;

    public AstrologyController(IAstrologyService service, AppDbContext db, IConfiguration cfg)
    {
        _service = service;
        _db = db;
        // Dedicated key preferred; falls back to the JWT key so it works out of the box.
        _encKey = cfg["Astrology:EncryptionKey"] ?? cfg["Jwt:Key"] ?? "astrology-fallback-key-set-in-env";
    }

    /// <summary>Compute a sidereal Rasi (D1) chart.</summary>
    /// <remarks>
    ///     POST /api/astrology/chart
    ///     { "year":1998, "month":1, "day":1, "hour":12, "minute":0, "second":0,
    ///       "timeZone":"Asia/Yangon", "latitude":16.8409, "longitude":96.1735 }
    /// </remarks>
    [HttpPost("chart")]
    [EnableRateLimiting("astrology")]
    [ProducesResponseType(typeof(ApiResponse<BirthChartData>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public IActionResult Chart([FromBody] BirthChartRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail(
                "Validation failed.", 400,
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var result = _service.ComputeRasiChart(req);
        return result.StatusCode switch
        {
            200 => Ok(result),
            400 => BadRequest(result),
            _   => StatusCode(result.StatusCode, result),
        };
    }

    // ── Remedy (yatra) / contact request — public, stored encrypted ──────────────
    [HttpPost("remedy-request")]
    [EnableRateLimiting("general")]
    public async Task<IActionResult> RemedyRequest([FromBody] RemedyRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400));

        var row = new RemedyRequest
        {
            Name = FieldCrypto.Encrypt(dto.Name, _encKey),
            Contact = FieldCrypto.Encrypt(dto.Contact, _encKey),
            Area = dto.Area,
            Message = FieldCrypto.Encrypt(dto.Message, _encKey),
            BirthInfo = FieldCrypto.Encrypt($"{dto.BirthDate} {dto.BirthTime}".Trim(), _encKey),
            Handled = false,
            CreatedAt = DateTime.UtcNow,
        };
        _db.RemedyRequests.Add(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { row.Id }, "Remedy request received."));
    }

    // ── Opt-in chart save — public, stored encrypted only WITH consent ──────────
    [HttpPost("save-chart")]
    [EnableRateLimiting("astrology")]
    public async Task<IActionResult> SaveChart([FromBody] SaveChartDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400));
        if (!dto.Consent)
            return Ok(ApiResponse<object>.Ok(new { stored = false }, "No consent — not stored."));

        var row = new QuerentChart
        {
            Name = FieldCrypto.Encrypt(dto.Name, _encKey),
            Gender = dto.Gender,
            BirthDate = FieldCrypto.Encrypt(dto.BirthDate, _encKey),
            BirthTime = FieldCrypto.Encrypt(dto.BirthTime, _encKey),
            TimeZone = dto.TimeZone,
            Location = FieldCrypto.Encrypt($"{dto.Latitude},{dto.Longitude}", _encKey),
            NayNan = dto.NayNan,
            Consent = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.QuerentCharts.Add(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { row.Id, stored = true }, "Saved."));
    }

    // ── Admin: remedy requests (decrypted) ──────────────────────────────────────
    [HttpGet("admin/remedies")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminRemedies()
    {
        var rows = await _db.RemedyRequests.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync();
        var view = rows.Select(r => new RemedyView
        {
            Id = r.Id,
            Name = FieldCrypto.Decrypt(r.Name, _encKey),
            Contact = FieldCrypto.Decrypt(r.Contact, _encKey),
            Area = r.Area,
            Message = FieldCrypto.Decrypt(r.Message, _encKey),
            BirthInfo = FieldCrypto.Decrypt(r.BirthInfo, _encKey),
            Handled = r.Handled,
            CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();
        return Ok(ApiResponse<List<RemedyView>>.Ok(view, "OK"));
    }

    // ── Admin: toggle handled ───────────────────────────────────────────────────
    [HttpPatch("admin/remedies/{id:int}/handled")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ToggleHandled(int id)
    {
        var row = await _db.RemedyRequests.FindAsync(id);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Not found.", 404));
        row.Handled = !row.Handled;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { row.Id, row.Handled }, "Updated."));
    }

    // ── Admin: saved querent charts (decrypted) ─────────────────────────────────
    [HttpGet("admin/charts")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminCharts()
    {
        var rows = await _db.QuerentCharts.OrderByDescending(c => c.CreatedAt).Take(500).ToListAsync();
        var view = rows.Select(c => new QuerentChartView
        {
            Id = c.Id,
            Name = FieldCrypto.Decrypt(c.Name, _encKey),
            Gender = c.Gender,
            BirthDate = FieldCrypto.Decrypt(c.BirthDate, _encKey),
            BirthTime = FieldCrypto.Decrypt(c.BirthTime, _encKey),
            TimeZone = c.TimeZone,
            Location = FieldCrypto.Decrypt(c.Location, _encKey),
            NayNan = c.NayNan,
            CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();
        return Ok(ApiResponse<List<QuerentChartView>>.Ok(view, "OK"));
    }
}
