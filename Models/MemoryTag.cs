using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.Models;

/// <summary>
/// A farewell message hung in the 3D Sanctuary. The message body is private:
/// only the author (matched by hashed OperatorToken) or the Admin can read it —
/// enforced server-side in SanctuaryController.
/// </summary>
public class MemoryTag
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string AuthorName { get; set; } = string.Empty;

    [Required, MaxLength(240)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Landmark { get; set; } = "tree";

    // 3D world position of the tag.
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }

    /// <summary>
    /// SHA-256 hash of the visitor's operator id (the raw id never touches the
    /// DB). Used to prove ownership for read/edit. Never serialized to clients.
    /// </summary>
    [Required, MaxLength(128)]
    public string OperatorToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
