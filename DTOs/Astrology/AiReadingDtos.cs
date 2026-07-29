using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Astrology;

/// <summary>
/// A compact, pre-interpreted snapshot of a computed chart, sent by the frontend
/// to the AI reading endpoint. Deliberately small — no raw ephemeris, only the
/// interpreted facts the model should reason over. No birth date/time is sent,
/// so the payload carries no re-identifiable birth PII.
/// </summary>
public class AiReadingRequestDto
{
    // ── Querent (optional, for a personal tone) ──────────────────────────────
    [MaxLength(80)] public string? Name { get; set; }
    [MaxLength(20)] public string? Gender { get; set; }
    [MaxLength(60)] public string? NayNan { get; set; }   // Myanmar birth-day sign

    // ── Core anchors ─────────────────────────────────────────────────────────
    [MaxLength(60)] public string? Ascendant { get; set; }   // e.g. "Simha (Leo)"
    [MaxLength(60)] public string? MoonSign { get; set; }     // Chandra rasi
    [MaxLength(60)] public string? SunSign { get; set; }

    /// <summary>Planet → sign / house placements (already interpreted).</summary>
    [MaxLength(20)]
    public List<PlacementDto> Placements { get; set; } = new();

    // ── Current Vimśottarī dasha context ─────────────────────────────────────
    [MaxLength(40)] public string? Mahadasha { get; set; }
    [MaxLength(40)] public string? Antardasha { get; set; }
    [MaxLength(40)] public string? Pratyantardasha { get; set; }
    [MaxLength(80)] public string? DashaWindow { get; set; }   // "2023-05 → 2026-01"

    // ── Sade Sati ────────────────────────────────────────────────────────────
    [MaxLength(80)] public string? SadeSatiStatus { get; set; } // "Active — peak phase" / "Not active"

    // ── Ashtakavarga ─────────────────────────────────────────────────────────
    /// <summary>Sarvashtakavarga total per sign (12 values, Aries→Pisces).</summary>
    public List<int>? SarvashtakavargaBySign { get; set; }
    [MaxLength(300)] public string? AshtakavargaNotes { get; set; } // "Strongest: Leo (34); weakest: Pisces (19)"

    // ── Optional extras the frontend may pass through ────────────────────────
    /// <summary>Active yogas by name (e.g. "Gaja Kesari Yoga").</summary>
    [MaxLength(30)]
    public List<string>? Yogas { get; set; }

    /// <summary>Life areas to emphasise (e.g. "Career", "Marriage").</summary>
    [MaxLength(12)]
    public List<string>? FocusAreas { get; set; }

    /// <summary>Any additional free-form context (current transits, notes).</summary>
    [MaxLength(2000)] public string? ExtraContext { get; set; }

    /// <summary>Reading language: "my" (Burmese, default) or "en".</summary>
    [MaxLength(8)] public string? Language { get; set; } = "my";
}

/// <summary>One interpreted planetary placement.</summary>
public class PlacementDto
{
    [MaxLength(30)] public string Planet { get; set; } = string.Empty;
    [MaxLength(30)] public string Sign { get; set; } = string.Empty;
    public int House { get; set; }
    [MaxLength(30)] public string? Nakshatra { get; set; }
    public bool Retrograde { get; set; }
    [MaxLength(30)] public string? Dignity { get; set; }   // exalted / debilitated / own / friend / …
}

/// <summary>The generated reading returned to the client.</summary>
public class AiReadingResponseDto
{
    public string Markdown { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Set when the reading was persisted to the signed-in account.</summary>
    public int? SavedId { get; set; }
}

/// <summary>A saved reading, listed for the account.</summary>
public class AiReadingView
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
