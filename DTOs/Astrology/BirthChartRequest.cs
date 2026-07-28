using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Astrology;

/// <summary>Birth details for a Vedic (sidereal) Rasi-chart calculation.</summary>
public class BirthChartRequest
{
    [Range(1, 3000)] public int Year { get; set; }
    [Range(1, 12)]   public int Month { get; set; }
    [Range(1, 31)]   public int Day { get; set; }
    [Range(0, 23)]   public int Hour { get; set; }
    [Range(0, 59)]   public int Minute { get; set; }
    [Range(0, 59)]   public int Second { get; set; }

    /// <summary>IANA time-zone id, e.g. "Asia/Yangon". Handles historical DST.</summary>
    [Required] public string TimeZone { get; set; } = "UTC";

    [Range(-90, 90)]   public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }

    /// <summary>Sidereal ayanamsa. MVP supports "lahiri" (default).</summary>
    public string Ayanamsa { get; set; } = "lahiri";
}
