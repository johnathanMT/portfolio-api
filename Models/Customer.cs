namespace PortfolioApi.Models;

/// <summary>A querent (customer) account — separate from admin Users. Email-only
/// sign-up with email confirmation. Password stored as a BCrypt hash.</summary>
public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;        // lowercase, unique
    public string Username { get; set; } = string.Empty;     // display name (editable)
    public string PasswordHash { get; set; } = string.Empty; // BCrypt, workFactor 12
    public bool EmailConfirmed { get; set; }
    public bool IsSuspended { get; set; }                    // admin can suspend logins
    public string? VerifyToken { get; set; }
    public DateTime? VerifyExpiry { get; set; }

    // ── Natal profile (the account owner's own birth chart) ─────────────────────
    // DOB / BirthTime / LocationName carry birth PII → AES-GCM encrypted at rest.
    // Latitude / Longitude / Timezone are needed for computation and kept plain.
    public string? Gender { get; set; }          // "male" | "female"
    public string? Dob { get; set; }             // encrypted yyyy-MM-dd
    public string? BirthTime { get; set; }       // encrypted HH:mm
    public string? LocationName { get; set; }    // encrypted place name
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Timezone { get; set; }        // IANA tz id, e.g. "Asia/Yangon"
    public DateTime? ProfileLastUpdated { get; set; }   // 90-day edit cooldown anchor

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
