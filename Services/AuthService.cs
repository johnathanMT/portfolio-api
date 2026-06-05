using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PortfolioApi.Common;
using PortfolioApi.DTOs.Auth;
using PortfolioApi.Interfaces;
using PortfolioApi.Models;

namespace PortfolioApi.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration  _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository   userRepo,
        IConfiguration    config,
        ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
        _config   = config;
        _logger   = logger;
    }

    // ──────────────────────────────────────────────────────────
    public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto)
    {
        // 1. Duplicate checks
        if (await _userRepo.EmailExistsAsync(dto.Email))
            return ApiResponse<AuthResponseDto>.Fail("Email is already registered.", 409);

        if (await _userRepo.UsernameExistsAsync(dto.Username))
            return ApiResponse<AuthResponseDto>.Fail("Username is already taken.", 409);

        // 2. Determine role via admin secret
        var adminSecret = _config["AdminSecret"];
        var role        = (!string.IsNullOrWhiteSpace(dto.AdminSecret) &&
                           dto.AdminSecret == adminSecret)
                          ? "Admin"
                          : "Guest";

        // 3. Hash password — BCrypt with cost factor 12
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12);

        // 4. Persist
        var user = new User
        {
            Username     = dto.Username.Trim(),
            Email        = dto.Email.ToLower().Trim(),
            PasswordHash = passwordHash,
            Role         = role,
        };

        await _userRepo.CreateAsync(user);
        _logger.LogInformation("New user registered: {Email} as {Role}", user.Email, role);

        // 5. Issue token immediately so the user doesn't need to log in again
        var token = GenerateJwtToken(user);
        return ApiResponse<AuthResponseDto>.Created(BuildResponse(user, token), "Registration successful.");
    }

    // ──────────────────────────────────────────────────────────
    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto)
    {
        var user = await _userRepo.GetByEmailAsync(dto.Email);

        // Use constant-time compare to resist timing attacks
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", dto.Email);
            return ApiResponse<AuthResponseDto>.Fail("Invalid email or password.", 401);
        }

        var token = GenerateJwtToken(user);
        _logger.LogInformation("User logged in: {Email}", user.Email);
        return ApiResponse<AuthResponseDto>.Ok(BuildResponse(user, token), "Login successful.");
    }

    // ──────────────────────────────────────────────────────────
    private string GenerateJwtToken(User user)
    {
        var jwtKey     = _config["Jwt:Key"]
                         ?? throw new InvalidOperationException("JWT Key not configured.");
        var issuer     = _config["Jwt:Issuer"]    ?? "PortfolioApi";
        var audience   = _config["Jwt:Audience"]  ?? "PortfolioApiUsers";
        var expHours   = int.Parse(_config["Jwt:ExpirationHours"] ?? "24");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email,          user.Email),
            new(ClaimTypes.Name,           user.Username),
            new(ClaimTypes.Role,           user.Role),
            // Standard JWT claims
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddHours(expHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthResponseDto BuildResponse(User user, string token)
    {
        var handler   = new JwtSecurityTokenHandler();
        var parsedJwt = handler.ReadJwtToken(token);

        return new AuthResponseDto
        {
            Token     = token,
            Username  = user.Username,
            Email     = user.Email,
            Role      = user.Role,
            ExpiresAt = parsedJwt.ValidTo,
        };
    }
}
