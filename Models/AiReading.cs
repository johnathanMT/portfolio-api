namespace PortfolioApi.Models;

/// <summary>An AI-generated Vedic reading saved under a customer account, so the
/// querent can revisit it. Title and Markdown are AES-GCM encrypted at rest
/// because the reading can contain the querent's name and personal details.</summary>
public class AiReading
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;     // encrypted
    public string Markdown { get; set; } = string.Empty;  // encrypted
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
