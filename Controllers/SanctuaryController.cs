using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortfolioApi.DTOs;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Sanctuary memory tags — CLEAN 3-LAYER:
///   Controller (HTTP) → IMemoryService (hashing/masking/sanitize/rules) → IMemoryRepository (EF Core).
///
///   GET  /api/sanctuary/memories       → PUBLIC; Message MASKED unless author/Admin.
///   POST /api/sanctuary/memories       → create/update ONE memory per operator (rate-limited).
///   GET  /api/sanctuary/admin/memories → ADMIN: every memory, UNMASKED.
///
/// Ownership comes from the raw operator id in the `X-Operator-Token` header
/// (the controller reads it; the service hashes it — the raw id never hits the DB).
/// Admin identity comes from the JWT `Admin` role, NOT anything the client can set.
/// </summary>
[ApiController]
[Route("api/sanctuary")]
public class SanctuaryController : ControllerBase
{
    private readonly IMemoryService _service;
    private readonly IValidator<CreateMemoryDto> _validator;

    public SanctuaryController(IMemoryService service, IValidator<CreateMemoryDto> validator)
    {
        _service = service; _validator = validator;
    }

    private string OperatorToken() => Request.Headers["X-Operator-Token"].ToString();

    [HttpGet("memories")]
    public async Task<IActionResult> GetMemories()
    {
        var views = await _service.GetMemoriesAsync(OperatorToken(), User.IsInRole("Admin"));
        var memories = views.Select(v => new
        {
            id = v.Id, author = v.Author, landmark = v.Landmark,
            position = new { x = v.X, y = v.Y, z = v.Z },
            createdAt = v.CreatedAt, mine = v.Mine, message = v.Message,
        });
        return Ok(new { success = true, memories });
    }

    [HttpPost("memories")]
    [EnableRateLimiting("memory-write")]
    public async Task<IActionResult> CreateMemory([FromBody] CreateMemoryDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { success = false, errors = validation.Errors.Select(e => e.ErrorMessage) });

        var result = await _service.SaveAsync(dto, OperatorToken());
        if (!result.Ok)
            return BadRequest(new { success = false, message = result.Error });

        return result.Edited
            ? Ok(new { success = true, id = result.Id, edited = true })
            : CreatedAtAction(nameof(GetMemories), new { id = result.Id }, new { success = true, id = result.Id });
    }

    [HttpGet("admin/memories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllForAdmin()
    {
        var views = await _service.GetAllForAdminAsync();
        var memories = views.Select(v => new
        {
            id = v.Id, author = v.Author, message = v.Message, landmark = v.Landmark,
            position = new { x = v.X, y = v.Y, z = v.Z }, createdAt = v.CreatedAt,
        }).ToList();
        return Ok(new { success = true, count = memories.Count, memories });
    }
}
