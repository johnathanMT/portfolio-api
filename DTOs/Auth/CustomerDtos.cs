using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Auth;

/// <summary>Customer sign-up — one comprehensive form: account + natal chart.
/// The natal fields are optional so a lightweight sign-up still works, but the new
/// premium form supplies them so the account renders its own chart immediately.</summary>
public class CustomerSignupDto
{
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = string.Empty;
    // Name of the account holder (stored as the display username).
    [Required, StringLength(100, MinimumLength = 2)] public string Username { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    [Required] public string ConfirmPassword { get; set; } = string.Empty;

    // ── Natal profile (optional) ────────────────────────────────────────────────
    [StringLength(20)] public string? Gender { get; set; }        // "male" | "female"
    [StringLength(20)] public string? Dob { get; set; }           // yyyy-MM-dd
    [StringLength(20)] public string? BirthTime { get; set; }     // HH:mm
    [StringLength(160)] public string? LocationName { get; set; }
    [Range(-90, 90)] public double? Latitude { get; set; }
    [Range(-180, 180)] public double? Longitude { get; set; }
    [StringLength(80)] public string? Timezone { get; set; }      // IANA tz id
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

/// <summary>Admin listing of a registered user account (with decrypted natal
/// profile so the admin can render the account's chart inline).</summary>
public class AdminUserView
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsSuspended { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool HasProfile { get; set; }
    public string CreatedAt { get; set; } = string.Empty;

    public string? Gender { get; set; }
    public string? Dob { get; set; }
    public string? BirthTime { get; set; }
    public string? LocationName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Timezone { get; set; }
}

/// <summary>Send a consultation chat message.</summary>
public class SendMessageDto
{
    [Required, StringLength(4000, MinimumLength = 1)] public string Text { get; set; } = string.Empty;
}

/// <summary>One chat message in a consultation thread.</summary>
public class MessageView
{
    public int Id { get; set; }
    public string SenderRole { get; set; } = string.Empty;   // Admin | Customer
    public string Text { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

/// <summary>Admin overview of a customer's consultation thread.</summary>
public class MessageThreadView
{
    public int CustomerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public string LastAt { get; set; } = string.Empty;
    public int Unread { get; set; }
}

/// <summary>Add / update the signed-in account's natal profile (and display name).</summary>
public class UpdateProfileDto
{
    [StringLength(100, MinimumLength = 2)] public string? Username { get; set; }
    [StringLength(20)] public string? Gender { get; set; }
    [StringLength(20)] public string? Dob { get; set; }
    [StringLength(20)] public string? BirthTime { get; set; }
    [StringLength(160)] public string? LocationName { get; set; }
    [Range(-90, 90)] public double? Latitude { get; set; }
    [Range(-180, 180)] public double? Longitude { get; set; }
    [StringLength(80)] public string? Timezone { get; set; }
}

/// <summary>The signed-in account + its natal profile (decrypted), returned by
/// GET /api/customer/me. HasProfile drives the "instant dashboard" on the front-end.</summary>
public class CustomerProfileView
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }

    public string? Gender { get; set; }
    public string? Dob { get; set; }
    public string? BirthTime { get; set; }
    public string? LocationName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Timezone { get; set; }
    public bool HasProfile { get; set; }
}

/// <summary>Resend confirmation — no validation attributes so the response is
/// always a constant, generic success (anti-enumeration).</summary>
public class ResendDto
{
    public string Email { get; set; } = string.Empty;
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
