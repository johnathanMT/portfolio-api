using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortfolioApi.DTOs;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Farewell "Digital Monument" RSVPs — CLEAN 3-LAYER:
///   Controller (HTTP) → IFarewellService (hash/sanitize/plot/rules) → IFarewellRepository (EF Core).
///
///   POST /api/farewell/rsvp        → create/update ONE monument per visitor (rate-limited).
///   GET  /api/farewell/plants      → PUBLIC list for the 3D world (no logistics).
///   GET  /api/farewell/admin/rsvps → ADMIN: full RSVP incl. dates + food.
///
/// The controller reads the `X-Operator-Token` header; the service hashes it.
/// </summary>
[ApiController]
[Route("api/farewell")]
public class FarewellController : ControllerBase
{
    private readonly IFarewellService _service;
    private readonly IValidator<CreateFarewellRsvpDto> _validator;

    public FarewellController(IFarewellService service, IValidator<CreateFarewellRsvpDto> validator)
    {
        _service = service; _validator = validator;
    }

    private string OperatorToken() => Request.Headers["X-Operator-Token"].ToString();

    [HttpPost("rsvp")]
    [EnableRateLimiting("memory-write")]
    public async Task<IActionResult> CreateRsvp([FromBody] CreateFarewellRsvpDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { success = false, errors = validation.Errors.Select(e => e.ErrorMessage) });

        var r = await _service.SaveAsync(dto, OperatorToken());
        if (!r.Ok) return BadRequest(new { success = false, message = r.Error });

        var payload = new
        {
            success = true,
            id = r.Id,
            name = r.Name,
            plantType = r.PlantType,
            position = new { x = r.X, y = r.Y, z = r.Z },
        };
        return r.Edited
            ? Ok(new { success = true, edited = true, payload.id, payload.name, payload.plantType, payload.position })
            : CreatedAtAction(nameof(GetPlants), new { id = r.Id }, payload);
    }

    [HttpGet("plants")]
    public async Task<IActionResult> GetPlants()
    {
        var views = await _service.GetPlantsAsync(OperatorToken());
        var plants = views.Select(v => new
        {
            id = v.Id, name = v.Name, message = v.Message, plantType = v.PlantType,
            position = new { x = v.X, y = v.Y, z = v.Z }, createdAt = v.CreatedAt, mine = v.Mine,
        });
        return Ok(new { success = true, plants });
    }

    [HttpGet("admin/rsvps")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllForAdmin()
    {
        var views = await _service.GetAllForAdminAsync();
        var rsvps = views.Select(v => new
        {
            id = v.Id, name = v.Name, message = v.Message, attending = v.Attending,
            datesAvailable = v.DatesAvailable, foodPreference = v.FoodPreference, plantType = v.PlantType,
            position = new { x = v.X, y = v.Y, z = v.Z }, createdAt = v.CreatedAt,
        }).ToList();
        return Ok(new { success = true, count = rsvps.Count, rsvps });
    }
}
