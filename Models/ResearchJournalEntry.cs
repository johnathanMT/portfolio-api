namespace PortfolioApi.Models;

/// <summary>A blind life-events journal entry owned by a customer account. Logged
/// WITHOUT looking at the predictions, so the later matching stays honest.</summary>
public class ResearchJournalEntry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }

    public string Month { get; set; } = string.Empty;       // yyyy-mm
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Magnitude { get; set; } = 2;                 // 1..3
    public string CreatedAt { get; set; } = string.Empty;   // client ISO

    public DateTime RowCreatedAt { get; set; } = DateTime.UtcNow;
}
