using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioApi.DTOs;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Poems for the homepage flip-book — CLEAN 3-LAYER:
///   Controller (HTTP) → IPoemService (business) → IPoemRepository (EF Core).
///
///   GET    /api/poetry       → PUBLIC.
///   GET    /api/poetry/{id}  → PUBLIC.
///   POST   /api/poetry       → ADMIN (JWT Role=Admin).
///   PUT    /api/poetry/{id}  → ADMIN.
///   DELETE /api/poetry/{id}  → ADMIN.
///
/// The controller's ONLY jobs: validate input, call the service, and shape the
/// HTTP response. No DbContext, no sanitization here.
/// </summary>
[ApiController]
[Route("api/poetry")]
public class PoetryController : ControllerBase
{
    private readonly IPoemService _service;
    private readonly IValidator<PoemDto> _validator;

    public PoetryController(IPoemService service, IValidator<PoemDto> validator)
    {
        _service = service; _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { success = true, poems = await _service.ListAsync() });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var poem = await _service.GetAsync(id);
        return poem is null
            ? NotFound(new { success = false, message = "Poem not found." })
            : Ok(new { success = true, poem });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] PoemDto dto)
    {
        if (await Invalid(dto) is { } bad) return bad;
        var poem = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetOne), new { id = poem.Id }, new { success = true, poem });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] PoemDto dto)
    {
        if (await Invalid(dto) is { } bad) return bad;
        var poem = await _service.UpdateAsync(id, dto);
        return poem is null
            ? NotFound(new { success = false, message = "Poem not found." })
            : Ok(new { success = true, poem });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id) =>
        await _service.DeleteAsync(id)
            ? Ok(new { success = true, id })
            : NotFound(new { success = false, message = "Poem not found." });

    // Validate the DTO; returns a 400 IActionResult when invalid, else null.
    private async Task<IActionResult?> Invalid(PoemDto dto)
    {
        var result = await _validator.ValidateAsync(dto);
        return result.IsValid ? null
            : BadRequest(new { success = false, errors = result.Errors.Select(e => e.ErrorMessage) });
    }
}
