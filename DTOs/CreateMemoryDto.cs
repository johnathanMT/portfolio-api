namespace PortfolioApi.DTOs;

/// <summary>Incoming payload for a new/edited Sanctuary memory.</summary>
public class CreateMemoryDto
{
    public string Author { get; set; } = string.Empty;   // matches the frontend's `author`
    public string Message { get; set; } = string.Empty;
    public string Landmark { get; set; } = "tree";
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
}
