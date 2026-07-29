namespace PortfolioApi.Models;

/// <summary>A pre-registered, falsifiable prediction owned by a customer account.
/// Mirrors the frontend research model. The immutable fields are SHA-256 hashed on
/// the client BEFORE it is sent, and the hash is stored here so tampering is
/// detectable — the server is storage, the client owns the lock.</summary>
public class ResearchPrediction
{
    public int Id { get; set; }
    public int CustomerId { get; set; }

    public string CreatedAt { get; set; } = string.Empty;   // ISO, MUST precede WindowStart
    public string WindowStart { get; set; } = string.Empty; // yyyy-mm-dd
    public string WindowEnd { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Claim { get; set; } = string.Empty;
    public string Falsifier { get; set; } = string.Empty;
    public double BaseRate { get; set; }
    public string BaseRateSource { get; set; } = string.Empty;
    public int Intensity { get; set; } = 3;   // 1..5
    public string Valence { get; set; } = "mixed";
    public string Hash { get; set; } = string.Empty;

    public string? Outcome { get; set; }       // hit | partial | miss
    public string? ReviewedAt { get; set; }
    public string? Note { get; set; }

    public DateTime RowCreatedAt { get; set; } = DateTime.UtcNow; // server clock, for ordering
}
