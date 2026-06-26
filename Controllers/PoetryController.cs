using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;
using PortfolioApi.Models;

namespace PortfolioApi.Controllers;

/// <summary>
/// Poems for the homepage flip-book.
///   GET    /api/poetry       → PUBLIC: all poems (newest first).
///   GET    /api/poetry/{id}  → PUBLIC: one poem.
///   POST   /api/poetry       → ADMIN: create.   ┐ protected by JWT Role=Admin
///   PUT    /api/poetry/{id}  → ADMIN: update.   │ (same policy as the Sanctuary
///   DELETE /api/poetry/{id}  → ADMIN: delete.   ┘  admin endpoints).
/// </summary>
[ApiController]
[Route("api/poetry")]
public class PoetryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IValidator<PoemDto> _validator;

    public PoetryController(AppDbContext db, IValidator<PoemDto> validator)
    {
        _db = db; _validator = validator;
    }

    // ── PUBLIC READ ──────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var poems = await _db.Poems
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedDate)
            .Select(p => new { p.Id, p.Title, p.Subtitle, p.Content, p.CreatedDate })
            .ToListAsync();
        return Ok(new { success = true, poems });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var poem = await _db.Poems.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Title, p.Subtitle, p.Content, p.CreatedDate })
            .FirstOrDefaultAsync();
        return poem is null ? NotFound(new { success = false, message = "Poem not found." }) : Ok(new { success = true, poem });
    }

    // ── ADMIN WRITE (JWT, Role=Admin) ────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] PoemDto dto)
    {
        var v = await _validator.ValidateAsync(dto);
        if (!v.IsValid) return BadRequest(new { success = false, errors = v.Errors.Select(e => e.ErrorMessage) });

        var poem = new Poem
        {
            Title       = Clean(dto.Title, 120),
            Subtitle    = Clean(dto.Subtitle, 80),
            Content     = CleanMultiline(dto.Content, 4000),
            CreatedDate = DateTime.UtcNow,
        };
        _db.Poems.Add(poem);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOne), new { id = poem.Id },
            new { success = true, poem = new { poem.Id, poem.Title, poem.Subtitle, poem.Content, poem.CreatedDate } });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] PoemDto dto)
    {
        var v = await _validator.ValidateAsync(dto);
        if (!v.IsValid) return BadRequest(new { success = false, errors = v.Errors.Select(e => e.ErrorMessage) });

        var poem = await _db.Poems.FindAsync(id);
        if (poem is null) return NotFound(new { success = false, message = "Poem not found." });

        poem.Title    = Clean(dto.Title, 120);
        poem.Subtitle = Clean(dto.Subtitle, 80);
        poem.Content  = CleanMultiline(dto.Content, 4000);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, poem = new { poem.Id, poem.Title, poem.Subtitle, poem.Content, poem.CreatedDate } });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var poem = await _db.Poems.FindAsync(id);
        if (poem is null) return NotFound(new { success = false, message = "Poem not found." });
        _db.Poems.Remove(poem);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, id });
    }

    // Single-line sanitize (titles): drop control chars + angle brackets, cap length.
    private static string Clean(string? s, int max)
    {
        s = (s ?? string.Empty).Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (!char.IsControl(ch) && ch != '<' && ch != '>') sb.Append(ch);
        var clean = sb.ToString();
        return clean.Length > max ? clean[..max] : clean;
    }

    // Multi-line sanitize (poem body): KEEP newlines, drop other control chars + < >.
    private static string CleanMultiline(string? s, int max)
    {
        s = (s ?? string.Empty).Replace("\r\n", "\n").Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (ch == '\n' || (!char.IsControl(ch) && ch != '<' && ch != '>')) sb.Append(ch);
        var clean = sb.ToString();
        return clean.Length > max ? clean[..max] : clean;
    }
}

/// <summary>Incoming payload for create/update.</summary>
public class PoemDto
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>Allow-list validation (auto-registered via AddValidatorsFromAssembly).</summary>
public class PoemDtoValidator : AbstractValidator<PoemDto>
{
    public PoemDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Subtitle).MaximumLength(80);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}
