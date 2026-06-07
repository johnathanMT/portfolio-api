using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioApi.Common;
using PortfolioApi.DTOs.Auth;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Administrative operations. All endpoints require the Admin role.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private static readonly string[] AllowedRoles = { "Guest", "Author", "Admin" };

    private readonly IUserRepository _users;

    public AdminController(IUserRepository users) => _users = users;

    // ──────────────────────────────────────────────────────────
    /// <summary>Change a user's role. Admin only.</summary>
    /// <remarks>
    /// Promote a trusted user to Author so they can create and manage their own articles.
    ///
    ///     PUT /api/admin/users/5/role
    ///     { "role": "Author" }
    /// </remarks>
    /// <response code="200">Role updated.</response>
    /// <response code="400">Invalid role value.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("users/{id:int}/role")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetUserRole(int id, [FromBody] UpdateRoleDto dto)
    {
        if (!AllowedRoles.Contains(dto.Role))
            return BadRequest(ApiResponse<object>.Fail(
                $"Invalid role '{dto.Role}'. Allowed values: {string.Join(", ", AllowedRoles)}.", 400));

        var user = await _users.GetByIdAsync(id);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail($"User {id} not found.", 404));

        user.Role = dto.Role;
        await _users.UpdateAsync(user);

        return Ok(ApiResponse<object>.Ok(
            new { user.Id, user.Username, user.Email, user.Role },
            $"Role updated to {dto.Role}."));
    }
}
