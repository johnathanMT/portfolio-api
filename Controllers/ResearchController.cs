using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioApi.Common;
using PortfolioApi.Data;
using PortfolioApi.DTOs.Research;
using PortfolioApi.Models;

namespace PortfolioApi.Controllers;

/// <summary>
/// Falsifiable-research store, persisted per customer account (replaces the
/// browser localStorage store on the frontend). Every endpoint is scoped to the
/// signed-in customer, so one account can never see another's predictions.
/// </summary>
[ApiController]
[Route("api/research")]
[Produces("application/json")]
[Authorize]
public class ResearchController : ControllerBase
{
    private readonly AppDbContext _db;
    public ResearchController(AppDbContext db) => _db = db;

    // ── GET /api/research/data — the whole dataset for this account ──────────────
    [HttpGet("data")]
    public async Task<IActionResult> GetData()
    {
        if (!TryCustomerId(out int cid))
            return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));

        var preds = await _db.ResearchPredictions
            .Where(p => p.CustomerId == cid)
            .OrderByDescending(p => p.RowCreatedAt)
            .ToListAsync();
        var journal = await _db.ResearchJournalEntries
            .Where(j => j.CustomerId == cid)
            .OrderByDescending(j => j.RowCreatedAt)
            .ToListAsync();

        var view = new ResearchDataView
        {
            Predictions = preds.Select(ToView).ToList(),
            Journal = journal.Select(ToView).ToList(),
        };
        return Ok(ApiResponse<ResearchDataView>.Ok(view, "OK"));
    }

    // ── POST /api/research/predictions — pre-register a prediction ───────────────
    [HttpPost("predictions")]
    public async Task<IActionResult> AddPrediction([FromBody] CreatePredictionDto dto)
    {
        if (!TryCustomerId(out int cid))
            return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400,
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var row = new ResearchPrediction
        {
            CustomerId = cid,
            CreatedAt = dto.CreatedAt,
            WindowStart = dto.WindowStart,
            WindowEnd = dto.WindowEnd,
            Area = dto.Area?.Trim() ?? string.Empty,
            Claim = dto.Claim.Trim(),
            Falsifier = dto.Falsifier.Trim(),
            BaseRate = dto.BaseRate,
            BaseRateSource = dto.BaseRateSource?.Trim() ?? string.Empty,
            Intensity = Math.Clamp(dto.Intensity, 1, 5),
            Valence = dto.Valence,
            Hash = dto.Hash ?? string.Empty,
            RowCreatedAt = DateTime.UtcNow,
        };
        _db.ResearchPredictions.Add(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<PredictionView>.Ok(ToView(row), "Locked."));
    }

    // ── PATCH /api/research/predictions/{id}/outcome — score after the window ────
    [HttpPatch("predictions/{id:int}/outcome")]
    public async Task<IActionResult> ReviewPrediction(int id, [FromBody] ReviewOutcomeDto dto)
    {
        if (!TryCustomerId(out int cid))
            return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));

        var outcome = dto.Outcome?.Trim().ToLowerInvariant();
        if (outcome is not ("hit" or "partial" or "miss"))
            return BadRequest(ApiResponse<object>.Fail("Outcome must be hit, partial, or miss.", 400));

        var row = await _db.ResearchPredictions.FirstOrDefaultAsync(p => p.Id == id && p.CustomerId == cid);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Prediction not found.", 404));

        row.Outcome = outcome;
        row.ReviewedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<PredictionView>.Ok(ToView(row), "Scored."));
    }

    // ── DELETE /api/research/predictions/{id} ───────────────────────────────────
    [HttpDelete("predictions/{id:int}")]
    public async Task<IActionResult> DeletePrediction(int id)
    {
        if (!TryCustomerId(out int cid))
            return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));

        var row = await _db.ResearchPredictions.FirstOrDefaultAsync(p => p.Id == id && p.CustomerId == cid);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Prediction not found.", 404));

        _db.ResearchPredictions.Remove(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.OkNoData("Deleted."));
    }

    // ── POST /api/research/journal — log a blind life event ─────────────────────
    [HttpPost("journal")]
    public async Task<IActionResult> AddJournal([FromBody] CreateJournalDto dto)
    {
        if (!TryCustomerId(out int cid))
            return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", 400,
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var row = new ResearchJournalEntry
        {
            CustomerId = cid,
            Month = dto.Month,
            Category = dto.Category?.Trim() ?? string.Empty,
            Description = dto.Description.Trim(),
            Magnitude = Math.Clamp(dto.Magnitude, 1, 3),
            CreatedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            RowCreatedAt = DateTime.UtcNow,
        };
        _db.ResearchJournalEntries.Add(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<JournalView>.Ok(ToView(row), "Added."));
    }

    // ── DELETE /api/research/journal/{id} ───────────────────────────────────────
    [HttpDelete("journal/{id:int}")]
    public async Task<IActionResult> DeleteJournal(int id)
    {
        if (!TryCustomerId(out int cid))
            return Unauthorized(ApiResponse<object>.Fail("Not a customer token.", 401));

        var row = await _db.ResearchJournalEntries.FirstOrDefaultAsync(j => j.Id == id && j.CustomerId == cid);
        if (row is null) return NotFound(ApiResponse<object>.Fail("Entry not found.", 404));

        _db.ResearchJournalEntries.Remove(row);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.OkNoData("Deleted."));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────
    private static PredictionView ToView(ResearchPrediction p) => new()
    {
        Id = p.Id.ToString(CultureInfo.InvariantCulture),
        CreatedAt = p.CreatedAt,
        WindowStart = p.WindowStart,
        WindowEnd = p.WindowEnd,
        Area = p.Area,
        Claim = p.Claim,
        Falsifier = p.Falsifier,
        BaseRate = p.BaseRate,
        BaseRateSource = p.BaseRateSource,
        Intensity = p.Intensity,
        Valence = p.Valence,
        Hash = p.Hash,
        Locked = true,
        Outcome = p.Outcome,
        ReviewedAt = p.ReviewedAt,
        Note = p.Note,
    };

    private static JournalView ToView(ResearchJournalEntry j) => new()
    {
        Id = j.Id.ToString(CultureInfo.InvariantCulture),
        Month = j.Month,
        Category = j.Category,
        Description = j.Description,
        Magnitude = j.Magnitude,
        CreatedAt = j.CreatedAt,
    };

    private bool TryCustomerId(out int id)
    {
        id = 0;
        if (User.FindFirst("ctype")?.Value != "customer") return false;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
    }
}
