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

/// <summary>A saved chart returned to the account owner (decrypted).</summary>
public class CustomerChartView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string BirthTime { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int NayNan { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
