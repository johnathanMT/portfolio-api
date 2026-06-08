using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortfolioApi.Common;
using PortfolioApi.DTOs.Interaction;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Anonymous article interactions: likes and fixed-set "quick reactions".
/// No authentication required; abuse is limited by a per-visitor dedupe key,
/// a server-side reaction allow-list, and rate limiting.
/// </summary>
[ApiController]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting("interactions")]
public class InteractionsController : ControllerBase
{
    private readonly IInteractionService _interactions;
    public InteractionsController(IInteractionService interactions) => _interactions = interactions;

    /// <summary>Anonymous visitor key — a client token if present, else the IP.</summary>
    private string VisitorKey()
        => Request.Headers["X-Visitor-Id"].FirstOrDefault()
           ?? HttpContext.Connection.RemoteIpAddress?.ToString()
           ?? "anonymous";

    [HttpPost("api/Articles/{id:int}/like")]
    public async Task<IActionResult> Like(int id)
    {
        var r = await _interactions.LikeAsync(id, VisitorKey());
        return StatusCode(r.StatusCode == 0 ? 200 : r.StatusCode, r);
    }

    [HttpDelete("api/Articles/{id:int}/like")]
    public async Task<IActionResult> Unlike(int id)
    {
        var r = await _interactions.UnlikeAsync(id, VisitorKey());
        return StatusCode(r.StatusCode == 0 ? 200 : r.StatusCode, r);
    }

    [HttpPost("api/Articles/{id:int}/reactions")]
    public async Task<IActionResult> React(int id, [FromBody] ReactionRequestDto dto)
    {
        var r = await _interactions.ReactAsync(id, dto.Reaction, VisitorKey());
        return StatusCode(r.StatusCode == 0 ? 200 : r.StatusCode, r);
    }
}
