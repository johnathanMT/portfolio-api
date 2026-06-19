using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;
using PortfolioApi.Models;

namespace PortfolioApi.Controllers;

/// <summary>
/// Farewell "Digital Monument" RSVPs.
///   POST /api/farewell/rsvp       → create/update ONE monument per visitor
///        (rate-limited, validated, sanitized). The server assigns a fixed plot
///        coordinate so plants never overlap; returns it so the client can play
///        the planting animation.
///   GET  /api/farewell/plants     → PUBLIC list for the 3D world (name, message,
///        plant type, position only — no logistics).
///   GET  /api/farewell/admin/rsvps → ADMIN ONLY: full RSVP incl. dates + food
///        preference, for planning the real event.
///
/// Ownership mirrors SanctuaryController: the client sends a raw operator id in
/// the `X-Operator-Token` header; the server stores only its SHA-256 hash.
/// </summary>
[ApiController]
[Route("api/farewell")]
public class FarewellController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<FarewellController> _logger;
    private readonly IValidator<CreateFarewellRsvpDto> _validator;

    public FarewellController(AppDbContext db, ILogger<FarewellController> logger, IValidator<CreateFarewellRsvpDto> validator)
    {
        _db = db; _logger = logger; _validator = validator;
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw ?? string.Empty)));

    // ── Memorial grove layout ────────────────────────────────────────────────
    // Each new monument takes the next free plot. Plots sit in a tidy arc in an
    // open area of the world; tune to taste. Y=0 (ground); the client lifts the
    // nametag above the plant. Coordinates are assigned ONCE and stored, so a
    // colleague editing their RSVP keeps the same spot.
    private static (float x, float y, float z) PlotForIndex(int i)
    {
        const int perRow = 8;
        const float gap = 7f;
        float startX = -((perRow - 1) * gap) / 2f;   // centre the row on X=0
        int row = i / perRow;
        int col = i % perRow;
        float x = startX + col * gap;
        float z = 44f + row * gap;                    // grove begins south of the plaza
        return (x, 0f, z);
    }

    // POST /api/farewell/rsvp
    [HttpPost("rsvp")]
    [EnableRateLimiting("memory-write")]
    public async Task<IActionResult> CreateRsvp([FromBody] CreateFarewellRsvpDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { success = false, errors = validation.Errors.Select(e => e.ErrorMessage) });

        var raw = Request.Headers["X-Operator-Token"].ToString();
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 200)
            return BadRequest(new { success = false, message = "Missing or invalid operator token." });
        var tokenHash = Hash(raw);

        var name    = Sanitize(dto.Name, 40);
        var message = Sanitize(dto.Message, 240);
        var dates   = Sanitize(dto.DatesAvailable, 120);
        var food    = Sanitize(dto.FoodPreference, 80);
        var plant   = (dto.PlantType ?? "sakura").Trim().ToLowerInvariant();
        if (name.Length == 0 || message.Length == 0)
            return BadRequest(new { success = false, message = "Name and message are required." });

        // ONE monument per visitor: edit in place, keep the original plot.
        var existing = await _db.FarewellRsvps.FirstOrDefaultAsync(f => f.OperatorToken == tokenHash);
        if (existing != null)
        {
            existing.Name           = name;
            existing.Message        = message;
            existing.DatesAvailable = dates;
            existing.FoodPreference = food;
            existing.PlantType      = plant;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                success = true,
                edited  = true,
                id      = existing.Id,
                name    = existing.Name,
                plantType = existing.PlantType,
                position  = new { x = existing.PositionX, y = existing.PositionY, z = existing.PositionZ },
            });
        }

        // Assign the next free plot. Count = current rows; plots fill in order.
        var count = await _db.FarewellRsvps.CountAsync();
        var (px, py, pz) = PlotForIndex(count);

        var rsvp = new FarewellRsvp
        {
            Name           = name,
            Message        = message,
            DatesAvailable = dates,
            FoodPreference = food,
            PlantType      = plant,
            PositionX      = px,
            PositionY      = py,
            PositionZ      = pz,
            OperatorToken  = tokenHash,
            CreatedAt      = DateTime.UtcNow,
        };
        _db.FarewellRsvps.Add(rsvp);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPlants), new { id = rsvp.Id }, new
        {
            success   = true,
            id        = rsvp.Id,
            name      = rsvp.Name,
            plantType = rsvp.PlantType,
            position  = new { x = rsvp.PositionX, y = rsvp.PositionY, z = rsvp.PositionZ },
        });
    }

    // GET /api/farewell/plants  — PUBLIC projection for the 3D world.
    [HttpGet("plants")]
    public async Task<IActionResult> GetPlants()
    {
        var callerHash = string.IsNullOrWhiteSpace(Request.Headers["X-Operator-Token"].ToString())
            ? null : Hash(Request.Headers["X-Operator-Token"].ToString());

        var plants = await _db.FarewellRsvps
            .AsNoTracking()
            .OrderBy(f => f.CreatedAt)
            .Select(f => new
            {
                id        = f.Id,
                name      = f.Name,
                message   = f.Message,
                plantType = f.PlantType,
                position  = new { x = f.PositionX, y = f.PositionY, z = f.PositionZ },
                createdAt = f.CreatedAt,
                mine      = callerHash != null && f.OperatorToken == callerHash,
            })
            .ToListAsync();

        return Ok(new { success = true, plants });
    }

    // GET /api/farewell/admin/rsvps — ADMIN ONLY: full logistics for event planning.
    [HttpGet("admin/rsvps")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllForAdmin()
    {
        var rsvps = await _db.FarewellRsvps
            .AsNoTracking()
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                id             = f.Id,
                name           = f.Name,
                message        = f.Message,
                datesAvailable = f.DatesAvailable,
                foodPreference = f.FoodPreference,
                plantType      = f.PlantType,
                position       = new { x = f.PositionX, y = f.PositionY, z = f.PositionZ },
                createdAt      = f.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { success = true, count = rsvps.Count, rsvps });
    }

    // Drop control characters + angle brackets → safe plain text; hard length cap.
    // (Same rules as SanctuaryController: NOT HTML-encoded, since React renders it
    //  as auto-escaped text — encoding here would surface literal entities.)
    private static string Sanitize(string? s, int max)
    {
        s = (s ?? string.Empty).Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (!char.IsControl(ch) && ch != '<' && ch != '>') sb.Append(ch);
        var clean = sb.ToString();
        return clean.Length > max ? clean[..max] : clean;
    }
}

/// <summary>Incoming payload for a farewell RSVP. Position is assigned server-side.</summary>
public class CreateFarewellRsvpDto
{
    public string Name { get; set; } = string.Empty;
    public string DatesAvailable { get; set; } = string.Empty;
    public string FoodPreference { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string PlantType { get; set; } = "sakura";
}

/// <summary>Allow-list validation (auto-registered via AddValidatorsFromAssembly).</summary>
public class CreateFarewellRsvpValidator : AbstractValidator<CreateFarewellRsvpDto>
{
    private static readonly string[] Plants = { "sakura", "orchid" };

    public CreateFarewellRsvpValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(240);
        RuleFor(x => x.DatesAvailable).MaximumLength(120);
        RuleFor(x => x.FoodPreference).MaximumLength(80);
        RuleFor(x => x.PlantType)
            .Must(p => Plants.Contains((p ?? string.Empty).Trim().ToLowerInvariant()))
            .WithMessage("Invalid plant type.");
    }
}
