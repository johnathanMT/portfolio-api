using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Auth;

/// <summary>Customer sign-up — email only, with password confirmation.</summary>
public class CustomerSignupDto
{
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 2)] public string Username { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    [Required] public string ConfirmPassword { get; set; } = string.Empty;
}

public class CustomerLoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateUsernameDto
{
    [Required, StringLength(100, MinimumLength = 2)] public string Username { get; set; } = string.Empty;
}
