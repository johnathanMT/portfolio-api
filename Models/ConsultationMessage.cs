namespace PortfolioApi.Models;

/// <summary>One message in a customer ↔ Sayar (admin) consultation thread. Used for
/// in-app remedy questions and follow-up chat, scoped to a single customer account.</summary>
public class ConsultationMessage
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string SenderRole { get; set; } = "Customer";   // "Admin" | "Customer"
    public string MessageText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
