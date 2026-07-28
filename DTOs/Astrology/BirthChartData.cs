namespace PortfolioApi.DTOs.Astrology;

/// <summary>A single planet's placement in the sidereal Rasi chart.</summary>
public class PlanetPosition
{
    public string Name { get; set; } = string.Empty;
    public double Longitude { get; set; }        // sidereal ecliptic longitude 0–360
    public int Sign { get; set; }                // 0 = Aries … 11 = Pisces
    public string SignName { get; set; } = string.Empty;
    public string SignNameSa { get; set; } = string.Empty;
    public double DegreeInSign { get; set; }     // 0–30
    public int Nakshatra { get; set; }           // 0–26
    public string NakshatraName { get; set; } = string.Empty;
    public int Pada { get; set; }                // 1–4
    public int House { get; set; }               // 1–12 (whole-sign from Ascendant)
    public bool Retrograde { get; set; }
    public string Dignity { get; set; } = "-";   // Exalted / Debilitated / Own / -

    // ── Phase 3: vargas / aspects / strength ──
    public int NavamsaSign { get; set; }          // D9 sign
    public string NavamsaSignName { get; set; } = string.Empty;
    public Dictionary<string, int> Vargas { get; set; } = new();   // D2,D3,D7,D9,D10,D12 → sign
    public int[] AspectsHouses { get; set; } = Array.Empty<int>();      // houses (1–12) aspected
    public string[] AspectsPlanets { get; set; } = Array.Empty<string>();
    public PlanetStrength? Strength { get; set; } // partial Shadbala; null for nodes
}

/// <summary>Partial Shadbala (Uccha + Dig + Naisargika bala), in virupas &amp; rupas.</summary>
public class PlanetStrength
{
    public double UcchaBala { get; set; }
    public double DigBala { get; set; }
    public double NaisargikaBala { get; set; }
    public double TotalVirupas { get; set; }
    public double TotalRupas { get; set; }
}

/// <summary>The Ascendant (Lagna) — first house cusp.</summary>
public class AscendantInfo
{
    public double Longitude { get; set; }
    public int Sign { get; set; }
    public string SignName { get; set; } = string.Empty;
    public string SignNameSa { get; set; } = string.Empty;
    public double DegreeInSign { get; set; }
    public int Nakshatra { get; set; }
    public string NakshatraName { get; set; } = string.Empty;
    public int Pada { get; set; }
    public int NavamsaSign { get; set; }
    public string NavamsaSignName { get; set; } = string.Empty;
}

/// <summary>Calculation metadata — what settings produced this chart.</summary>
public class ChartMeta
{
    public string Ayanamsa { get; set; } = "Lahiri";
    public string HouseSystem { get; set; } = "Whole Sign";
    public double JulianDayUt { get; set; }
    public string UtcIso { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>A Vimshottari mahadasha (planetary ruling period).</summary>
public class DashaPeriod
{
    public string Lord { get; set; } = string.Empty;
    public string StartUtc { get; set; } = string.Empty;   // yyyy-MM-dd
    public string EndUtc { get; set; } = string.Empty;
    public double Years { get; set; }
}

/// <summary>Full sidereal Rasi (D1) chart payload.</summary>
public class BirthChartData
{
    public AscendantInfo Ascendant { get; set; } = new();
    public List<PlanetPosition> Planets { get; set; } = new();
    public List<DashaPeriod> Dashas { get; set; } = new();
    public ChartMeta Meta { get; set; } = new();
}
