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
/// Sanctuary memory tags.
///   GET  /api/sanctuary/memories      → all tags; Message is MASKED to
///        "🔒 Private Message" unless the caller is the author or the Admin.
///   POST /api/sanctuary/memories      → create/update ONE memory per operator
///        (rate-limited, validated, sanitized).
///
/// Ownership: the client sends its raw operator id in the `X-Operator-Token`
/// header; the server stores only its SHA-256 hash, so the raw id never lands in
/// the DB and can't be read back. Admin identity comes from the JWT `Admin` role
/// (HttpOnly cookie — see SECURITY.md), NOT from anything the client can set.
/// </summary>
[ApiController]
[Route("api/sanctuary")]
public class SanctuaryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SanctuaryController> _logger;
    private readonly IValidator<CreateMemoryDto> _validator;

    public SanctuaryController(AppDbContext db, ILogger<SanctuaryController> logger, IValidator<CreateMemoryDto> validator)
    {
        _db = db; _logger = logger; _validator = validator;
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw ?? string.Empty)));

    private string? CallerHash()
    {
        var raw = Request.Headers["X-Operator-Token"].ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : Hash(raw);
    }

    // GET /api/sanctuary/memories
    [HttpGet("memories")]
    public async Task<IActionResult> GetMemories()
    {
        var callerHash = CallerHash();
        var isAdmin = User.IsInRole("Admin");

        var memories = await _db.MemoryTags
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                id = m.Id,
                author = m.AuthorName,
                landmark = m.Landmark,
                position = new { x = m.PositionX, y = m.PositionY, z = m.PositionZ },
                createdAt = m.CreatedAt,
                mine = callerHash != null && m.OperatorToken == callerHash,
                // ── SERVER-SIDE PRIVACY MASKING ──
                message = (isAdmin || (callerHash != null && m.OperatorToken == callerHash))
                          ? m.Message
                          : "🔒 Private Message",
            })
            .ToListAsync();

        return Ok(new { success = true, memories });
    }

    // POST /api/sanctuary/memories  (one per operator → create or edit-in-place)
    [HttpPost("memories")]
    [EnableRateLimiting("memory-write")]
    public async Task<IActionResult> CreateMemory([FromBody] CreateMemoryDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { success = false, errors = validation.Errors.Select(e => e.ErrorMessage) });

        var raw = Request.Headers["X-Operator-Token"].ToString();
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 200)
            return BadRequest(new { success = false, message = "Missing or invalid operator token." });
        var tokenHash = Hash(raw);

        var author = Sanitize(dto.Author, 40);
        var message = Sanitize(dto.Message, 240);
        if (author.Length == 0 || message.Length == 0)
            return BadRequest(new { success = false, message = "Name and message are required." });

        // ONE memory per operator: edit the existing one instead of adding another.
        var existing = await _db.MemoryTags.FirstOrDefaultAsync(m => m.OperatorToken == tokenHash);
        if (existing != null)
        {
            existing.AuthorName = author;
            existing.Message = message;
            existing.Landmark = dto.Landmark;
            existing.PositionX = dto.PositionX;
            existing.PositionY = dto.PositionY;
            existing.PositionZ = dto.PositionZ;
            await _db.SaveChangesAsync();
            return Ok(new { success = true, id = existing.Id, edited = true });
        }

        var tag = new MemoryTag
        {
            AuthorName = author,
            Message = message,
            Landmark = dto.Landmark,
            PositionX = dto.PositionX,
            PositionY = dto.PositionY,
            PositionZ = dto.PositionZ,
            OperatorToken = tokenHash,
            CreatedAt = DateTime.UtcNow,
        };
        _db.MemoryTags.Add(tag);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMemories), new { id = tag.Id }, new { success = true, id = tag.Id });
    }

    // GET /api/sanctuary/admin/memories  — ADMIN ONLY: every memory, UNMASKED.
    // Requires a valid JWT with the "Admin" role (Authorization: Bearer <token>).
    [HttpGet("admin/memories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllForAdmin()
    {
        var memories = await _db.MemoryTags
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                id = m.Id,
                author = m.AuthorName,
                message = m.Message,                 // full text — never masked for admin
                landmark = m.Landmark,
                position = new { x = m.PositionX, y = m.PositionY, z = m.PositionZ },
                createdAt = m.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { success = true, count = memories.Count, memories });
    }

    // Drop control characters + angle brackets → safe plain text; hard length cap.
    private static string Sanitize(string? s, int max)
    {
        s = (s ?? string.Empty).Trim();
        var sb = new StringBuilder(s.Length);
        // Drop control chars + angle brackets (no HTML can be stored). We do NOT
        // HTML-encode: the client renders this as React text (auto-escaped), so
        // encoding here would surface literal entities like "it&#39;s".
        foreach (var ch in s)
            if (!char.IsControl(ch) && ch != '<' && ch != '>') sb.Append(ch);
        var clean = sb.ToString();
        return clean.Length > max ? clean[..max] : clean;
    }
}

/// <summary>Incoming payload for a new/edited memory.</summary>
public class CreateMemoryDto
{
    public string Author { get; set; } = string.Empty;   // matches the frontend's `author`
    public string Message { get; set; } = string.Empty;
    public string Landmark { get; set; } = "tree";
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
}

/// <summary>Allow-list validation (auto-registered via AddValidatorsFromAssembly).</summary>
public class CreateMemoryValidator : AbstractValidator<CreateMemoryDto>
{
    // Accepts both the legacy zone keys and the specific clickable-building keys
    // sent by the frontend (SCENE_LAYOUT). Keep in sync with PLACE_KEYS / BUILDING_LABEL.
    private static readonly string[] Landmarks =
    {
        // legacy zones
        "tree", "ship", "village", "castle", "plaza",
        // clickable buildings
        "sakura", "torii", "bagan", "ferris_wheel", "jp_castle",
        "castle_sakura", "plaza_night", "hospital", "london_university",
    };

    public CreateMemoryValidator()
    {
        RuleFor(x => x.Author).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(240);
        RuleFor(x => x.Landmark).Must(l => Landmarks.Contains(l)).WithMessage("Invalid landmark.");
        RuleFor(x => x.PositionX).InclusiveBetween(-200f, 200f);
        RuleFor(x => x.PositionY).InclusiveBetween(-50f, 100f);
        RuleFor(x => x.PositionZ).InclusiveBetween(-200f, 200f);
    }
}
