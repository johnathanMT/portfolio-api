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

    // ── Identity for the 30-day rate-limit hash (used only to derive a SHA-256
    //    key; the raw values are stored ONLY inside the encrypted payload). ──────
    [MaxLength(20)] public string? BirthDate { get; set; }   // yyyy-mm-dd
    [MaxLength(20)] public string? BirthTime { get; set; }   // HH:mm
    [MaxLength(160)] public string? Location { get; set; }
}

/// <summary>Look up an existing reading request by querent identity.</summary>
public class ReadingStatusQueryDto
{
    [MaxLength(80)] public string? Name { get; set; }
    [MaxLength(20)] public string? BirthDate { get; set; }
    [MaxLength(20)] public string? BirthTime { get; set; }
    [MaxLength(160)] public string? Location { get; set; }
}

/// <summary>Status of a reading request returned to the querent.</summary>
public class ReadingStatusView
{
    /// <summary>None | Pending | Approved | Rejected</summary>
    public string Status { get; set; } = "None";
    public int RequestId { get; set; }
    public string? Markdown { get; set; }   // only when Approved
    public string? Model { get; set; }
    public bool PdfRequested { get; set; }
    public bool AlreadyRequested { get; set; }   // true if this request already existed within 30 days
    public string CreatedAt { get; set; } = string.Empty;
    public string? ApprovedAt { get; set; }
}

/// <summary>Admin listing row for pending/approved reading requests. When the
/// request came from a signed-in account, the account's decrypted natal profile
/// is attached so the Sayar has full context.</summary>
public class ReadingRequestAdminView
{
    public int Id { get; set; }
    public string QuerentName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasMarkdown { get; set; }
    public bool PdfRequested { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? ApprovedAt { get; set; }

    // ── Registered-account context (null for guest requests) ────────────────────
    public bool IsRegistered { get; set; }
    public string? AccountEmail { get; set; }
    public string? AccountUsername { get; set; }
    public string? Gender { get; set; }
    public string? Dob { get; set; }
    public string? BirthTime { get; set; }
    public string? LocationName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Timezone { get; set; }
}

/// <summary>Body for a querent's PDF request — the email to deliver the PDF to.</summary>
public class RequestPdfEmailDto
{
    [System.ComponentModel.DataAnnotations.MaxLength(160)]
    public string? Email { get; set; }
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
