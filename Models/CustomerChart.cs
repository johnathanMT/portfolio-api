namespace PortfolioApi.Models;

/// <summary>A birth chart saved under a customer account, so they never re-enter
/// their details. PII fields are AES-GCM encrypted at rest.</summary>
public class CustomerChart
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;       // encrypted
    public string Gender { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;  // encrypted
    public string BirthTime { get; set; } = string.Empty;  // encrypted
    public string TimeZone { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;   // encrypted "lat,lon"
    public int NayNan { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
