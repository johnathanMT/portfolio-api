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
    private ApiResponse<object> BuildValidationError() =>
        ApiResponse<object>.Fail(
            "Validation failed.",
            400,
            ModelState.Values
                      .SelectMany(v => v.Errors)
                      .Select(e => e.ErrorMessage)
                      .ToList());
}
