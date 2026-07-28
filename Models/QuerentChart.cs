namespace PortfolioApi.Models;

/// <summary>A stored birth chart (opt-in consent only) to help the Sayar's
/// readings. PII fields (Name, BirthDate, BirthTime, Location) are AES-GCM
/// encrypted at rest.</summary>
public class QuerentChart
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;       // encrypted
    public string Gender { get; set; } = string.Empty;     // non-sensitive
    public string BirthDate { get; set; } = string.Empty;  // encrypted
    public string BirthTime { get; set; } = string.Empty;  // encrypted
    public string TimeZone { get; set; } = string.Empty;   // non-sensitive
    public string Location { get; set; } = string.Empty;   // encrypted "lat,lon"
    public int NayNan { get; set; }
    public bool Consent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
