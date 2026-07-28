using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Astrology;

/// <summary>Public remedy/contact submission.</summary>
public class RemedyRequestDto
{
    [StringLength(120)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(160)] public string Contact { get; set; } = string.Empty;
    [StringLength(120)] public string Area { get; set; } = string.Empty;
    [StringLength(2000)] public string Message { get; set; } = string.Empty;
    [StringLength(20)] public string BirthDate { get; set; } = string.Empty;
    [StringLength(10)] public string BirthTime { get; set; } = string.Empty;
}

/// <summary>Opt-in chart save (only stored when Consent == true).</summary>
public class SaveChartDto
{
    [StringLength(120)] public string Name { get; set; } = string.Empty;
    [StringLength(20)] public string Gender { get; set; } = string.Empty;
    [StringLength(20)] public string BirthDate { get; set; } = string.Empty;
    [StringLength(10)] public string BirthTime { get; set; } = string.Empty;
    [StringLength(80)] public string TimeZone { get; set; } = string.Empty;
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
    public int NayNan { get; set; }
    public bool Consent { get; set; }
}

/// <summary>Admin view — decrypted remedy request.</summary>
public class RemedyView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string BirthInfo { get; set; } = string.Empty;
    public bool Handled { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>Admin view — decrypted saved chart.</summary>
public class QuerentChartView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string BirthTime { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int NayNan { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
