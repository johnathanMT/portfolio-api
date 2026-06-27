namespace PortfolioApi.DTOs;

/// <summary>Incoming payload for creating / updating a poem.</summary>
public class PoemDto
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
