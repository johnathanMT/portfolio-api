namespace PortfolioApi.Models;

/// <summary>A request to receive the Vedin reading PDF by email, behind admin
/// approval + a one-time secure download token. PII is AES-GCM encrypted.</summary>
public class PdfRequest
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;      // encrypted
    public string Name { get; set; } = string.Empty;       // encrypted
    public string BirthInfo { get; set; } = string.Empty;  // encrypted "date time"
    public string ApprovalStatus { get; set; } = "Pending"; // Pending | Approved | Downloaded
    public string DownloadToken { get; set; } = string.Empty;
    public DateTime? TokenExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
