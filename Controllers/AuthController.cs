using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortfolioApi.Common;
using PortfolioApi.DTOs.Auth;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Handles user registration and authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) =>
        _authService = authService;

    // ──────────────────────────────────────────────────────────
    /// <summary>Register a new user account.</summary>
    /// <remarks>
    /// Supply the optional <c>AdminSecret</c> field (configured on the server)
    /// to create an Admin account. Without it, the account is a Guest.
    ///
    ///     POST /api/auth/register
    ///     {
    ///         "username": "myo",
    ///         "email": "myo@example.com",
    ///         "password": "Str0ng!Pass",
    ///         "adminSecret": "OPTIONAL_SERVER_SECRET"
    ///     }
    /// </remarks>
    /// <response code="201">Account created; JWT returned.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="409">Email or username already taken.</response>
    /// <response code="429">Too many requests — rate limit reached.</response>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationError());

        var result = await _authService.RegisterAsync(dto);
        return result.StatusCode switch
        {
            201 => StatusCode(201, result),
            409 => Conflict(result),
            _   => BadRequest(result),
        };
    }

    // ──────────────────────────────────────────────────────────
    /// <summary>Log in with email and password.</summary>
    /// <remarks>
    ///     POST /api/auth/login
    ///     {
    ///         "email": "myo@example.com",
    ///         "password": "Str0ng!Pass"
    ///     }
    /// </remarks>
    /// <response code="200">JWT token returned.</response>
    /// <response code="401">Invalid credentials.</response>
    /// <response code="429">Too many requests — rate limit reached.</response>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationError());

        var result = await _authService.LoginAsync(dto);
        return result.StatusCode switch
        {
            200 => Ok(result),
            _   => Unauthorized(result),
        };
    }

    // ──────────────────────────────────────────────────────────
    /// <summary>Change your own password (must be signed in).</summary>
    /// <remarks>
    ///     POST /api/auth/change-password
    ///     Authorization: Bearer &lt;jwt&gt;
    ///     { "currentPassword": "OldP@ss1", "newPassword": "NewStr0ng!Pass" }
    /// </remarks>
    /// <response code="200">Password updated.</response>
    /// <response code="400">Validation error / new password same as old.</response>
    /// <response code="401">Not signed in, or current password incorrect.</response>
    /// <response code="429">Too many requests — rate limit reached.</response>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationError());

        // Identity comes from the verified JWT, never the request body.
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid or missing authentication token.", 401));

        var result = await _authService.ChangePasswordAsync(userId, dto);
        return result.StatusCode switch
        {
            200 => Ok(result),
            401 => Unauthorized(result),
            404 => NotFound(result),
            _   => BadRequest(result),
        };
    }

    // ──────────────────────────────────────────────────────────
    private ApiResponse<object> BuildValidationError() =>
        ApiResponse<object>.Fail(
            "Validation failed.",
            400,
            ModelState.Values
                      .SelectMany(v => v.Errors)
                      .Select(e => e.ErrorMessage)
                      .ToList());
}
