namespace PortfolioApi.Models;

/// <summary>A visitor's remedy (yatra) / contact request to the Sayar.
/// PII fields (Name, Contact, Message, BirthInfo) are stored AES-GCM encrypted.</summary>
public class RemedyRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;       // encrypted
    public string Contact { get; set; } = string.Empty;    // encrypted
    public string Area { get; set; } = string.Empty;       // non-sensitive label
    public string Message { get; set; } = string.Empty;    // encrypted
    public string BirthInfo { get; set; } = string.Empty;  // encrypted "date time"
    public bool Handled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
