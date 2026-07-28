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
    public bool Combust { get; set; }            // asta — too close to the Sun
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
    public double PakshaBala { get; set; }
    public double DrikBala { get; set; }
    public double TotalVirupas { get; set; }
    public double TotalRupas { get; set; }
}

/// <summary>A detected yoga (planetary combination) with the planets involved.</summary>
public class Yoga
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Planets { get; set; } = Array.Empty<string>();
}

/// <summary>One rule-based finding behind a prediction. The frontend maps the
/// Code (+ Planet/House/Value) into localized EN/MM sentences.</summary>
public class Finding
{
    public string Code { get; set; } = string.Empty;   // lordDignity | karakaDignity | occupant | dashaActive
    public string Planet { get; set; } = string.Empty;
    public int House { get; set; }
    public string Value { get; set; } = string.Empty;  // Exalted | Debilitated | Own | Neutral | benefic | malefic | area
}

/// <summary>A per-life-area prediction: overall tone + score + the findings that produced it.</summary>
public class AreaPrediction
{
    public string Area { get; set; } = string.Empty;   // love | career | education | social | health | wealth
    public string Tone { get; set; } = "mixed";        // favorable | mixed | testing
    public int Score { get; set; }
    public List<Finding> Findings { get; set; } = new();
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

/// <summary>Ashtakavarga — per-sign benefic points (bindus). Bav = each planet's
/// Bhinnashtakavarga (12 signs); Sav = Sarvashtakavarga (sum across 7 planets).</summary>
public class AshtakavargaData
{
    public Dictionary<string, int[]> Bav { get; set; } = new();   // planet → 12 signs
    public int[] Sav { get; set; } = new int[12];                 // 12 signs, total 337
}

/// <summary>A transiting planet's position for a given year (gochara).</summary>
public class TransitPos
{
    public string Planet { get; set; } = string.Empty;     // Jupiter | Saturn | Rahu
    public int Sign { get; set; }                           // 0–11
    public string SignName { get; set; } = string.Empty;
    public int HouseFromLagna { get; set; }                // 1–12
    public int HouseFromMoon { get; set; }                 // 1–12 (Chandra Lagna)
}

/// <summary>A structured transit/period note; the frontend localizes it to EN/MM.</summary>
public class TransitNote
{
    public string Tone { get; set; } = "info";             // good | warn | info
    public string Code { get; set; } = string.Empty;       // jupLagna | jupTrineMoon | sadeSati | ashtamaSani | rahuReturn …
    public string Planet { get; set; } = string.Empty;
    public int House { get; set; }
}

/// <summary>One year of the life timeline: age, active dasha/bhukti, transits and notes.</summary>
public class YearForecast
{
    public int Year { get; set; }
    public int Age { get; set; }
    public string Maha { get; set; } = string.Empty;       // running mahadasha lord
    public string Bhukti { get; set; } = string.Empty;     // running antardasha lord
    public int Stars { get; set; }                         // 1–5 overall favourability
    public bool SadeSati { get; set; }
    public List<TransitPos> Transits { get; set; } = new();
    public List<TransitNote> Notes { get; set; } = new();
}

/// <summary>Full sidereal Rasi (D1) chart payload.</summary>
public class BirthChartData
{
    public AscendantInfo Ascendant { get; set; } = new();
    public List<PlanetPosition> Planets { get; set; } = new();
    public List<DashaPeriod> Dashas { get; set; } = new();
    public List<DashaPeriod> Antardashas { get; set; } = new();      // bhuktis of the current mahadasha
    public List<DashaPeriod> Pratyantardashas { get; set; } = new(); // pratyantars of the current bhukti (3rd level)
    public List<Yoga> Yogas { get; set; } = new();
    public List<AreaPrediction> Predictions { get; set; } = new();
    public List<YearForecast> Timeline { get; set; } = new();   // whole-life age → dasha/transit forecast
    public AshtakavargaData Ashtakavarga { get; set; } = new();
    public ChartMeta Meta { get; set; } = new();
}
