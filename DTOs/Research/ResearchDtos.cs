using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Research;

/// <summary>The whole research dataset for a signed-in account.</summary>
public class ResearchDataView
{
    public List<PredictionView> Predictions { get; set; } = new();
    public List<JournalView> Journal { get; set; } = new();
}

public class PredictionView
{
    public string Id { get; set; } = string.Empty;   // server id as string (frontend key)
    public string CreatedAt { get; set; } = string.Empty;
    public string WindowStart { get; set; } = string.Empty;
    public string WindowEnd { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Claim { get; set; } = string.Empty;
    public string Falsifier { get; set; } = string.Empty;
    public double BaseRate { get; set; }
    public string BaseRateSource { get; set; } = string.Empty;
    public int Intensity { get; set; }
    public string Valence { get; set; } = "mixed";
    public string Hash { get; set; } = string.Empty;
    public bool Locked { get; set; } = true;
    public string? Outcome { get; set; }
    public string? ReviewedAt { get; set; }
    public string? Note { get; set; }
}

public class JournalView
{
    public string Id { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Magnitude { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>Create a pre-registered prediction (id + row-time assigned server-side).</summary>
public class CreatePredictionDto
{
    [Required, MaxLength(40)] public string CreatedAt { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string WindowStart { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string WindowEnd { get; set; } = string.Empty;
    [MaxLength(120)] public string? Area { get; set; }
    [Required, MaxLength(2000)] public string Claim { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Falsifier { get; set; } = string.Empty;
    [Range(0, 1)] public double BaseRate { get; set; }
    [MaxLength(255)] public string? BaseRateSource { get; set; }
    [Range(1, 5)] public int Intensity { get; set; } = 3;
    [MaxLength(20)] public string Valence { get; set; } = "mixed";
    [MaxLength(80)] public string? Hash { get; set; }
}

public class ReviewOutcomeDto
{
    /// <summary>hit | partial | miss</summary>
    [Required, MaxLength(20)] public string Outcome { get; set; } = string.Empty;
}

public class CreateJournalDto
{
    [Required, MaxLength(10)] public string Month { get; set; } = string.Empty;
    [MaxLength(120)] public string? Category { get; set; }
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [Range(1, 3)] public int Magnitude { get; set; } = 2;
}
