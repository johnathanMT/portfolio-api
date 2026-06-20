using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.Models;

/// <summary>
/// A colleague's farewell RSVP + the "living monument" it plants in the 3D
/// Sanctuary. Kept SEPARATE from <see cref="MemoryTag"/> on purpose:
///   • MemoryTag = a private message hung in the world (masked, one-per-operator,
///     edit-in-place). Mixing a Type discriminator + nullable RSVP columns onto it
///     would pollute that tight masking/ownership logic.
///   • FarewellRsvp = event-logistics (dates, food) + a PUBLIC monument (name,
///     message, plant type, fixed plot coordinate). Different lifecycle, different
///     audience (logistics are admin-only), so it earns its own table.
///
/// The 3D world reads only the PUBLIC projection (Name, Message, PlantType,
/// Position); the Admin dashboard reads the logistics fields too.
/// </summary>
public class FarewellRsvp
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the colleague can attend the farewell party. Everyone can
    /// still plant a tree; only attendees provide dates + food.</summary>
    public bool Attending { get; set; } = true;

    /// <summary>Free-text list of dates the colleague is available (e.g. "Jul 4, Jul 6").
    /// Optional — empty when not attending.</summary>
    [MaxLength(120)]
    public string DatesAvailable { get; set; } = string.Empty;

    /// <summary>Food preference / dietary note (e.g. "Vegetarian", "No pork").
    /// Optional — empty when not attending.</summary>
    [MaxLength(80)]
    public string FoodPreference { get; set; } = string.Empty;

    /// <summary>The short farewell message displayed on the monument.</summary>
    [Required, MaxLength(240)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Which plant to grow. Validated against an allow-list ("sakura" | "orchid").</summary>
    [MaxLength(24)]
    public string PlantType { get; set; } = "sakura";

    // Fixed plot in the 3D "memorial grove" — assigned ONCE by the server at
    // creation so plants never overlap, and kept stable across edits.
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }

    /// <summary>
    /// SHA-256 hash of the visitor's operator id (raw id never touches the DB).
    /// Enforces one monument per person (edit-in-place) and ownership. Never
    /// serialized to clients.
    /// </summary>
    [Required, MaxLength(128)]
    public string OperatorToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
