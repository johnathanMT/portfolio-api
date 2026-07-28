using SwissEphNet;
using PortfolioApi.Common;
using PortfolioApi.DTOs.Astrology;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Services;

/// <summary>
/// Vedic (sidereal) Rasi-chart calculator built on Swiss Ephemeris (SwissEphNet).
///
///  • Uses the built-in MOSHIER model (SEFLG_MOSEPH) → NO ephemeris data files
///    need to be shipped/deployed. Accuracy is well within astrological needs.
///  • Sidereal zodiac with the LAHIRI ayanamsa (Jyotish standard).
///  • WHOLE-SIGN houses (the classical Jyotish default): the sign holding the
///    Ascendant is the 1st house, and each subsequent sign is the next house.
///  • Rahu = mean lunar node; Ketu = 180° opposite. Nodes are always retrograde.
///
/// The calculation is a PURE function of the birth details — no DB, deterministic,
/// and trivially cacheable/unit-testable (compare against Jagannatha Hora, etc.).
/// </summary>
public class AstrologyService : IAstrologyService
{
    private static readonly string[] Signs =
        { "Aries","Taurus","Gemini","Cancer","Leo","Virgo","Libra","Scorpio","Sagittarius","Capricorn","Aquarius","Pisces" };

    private static readonly string[] SignsSa =
        { "Mesha","Vrishabha","Mithuna","Karka","Simha","Kanya","Tula","Vrishchika","Dhanu","Makara","Kumbha","Meena" };

    private static readonly string[] Nakshatras =
    {
        "Ashwini","Bharani","Krittika","Rohini","Mrigashira","Ardra","Punarvasu","Pushya","Ashlesha",
        "Magha","Purva Phalguni","Uttara Phalguni","Hasta","Chitra","Swati","Vishakha","Anuradha","Jyeshtha",
        "Mula","Purva Ashadha","Uttara Ashadha","Shravana","Dhanishta","Shatabhisha","Purva Bhadrapada","Uttara Bhadrapada","Revati"
    };

    // Graha id (Swiss Eph) → display name, in traditional order.
    private static readonly (int Id, string Name)[] Grahas =
    {
        (SwissEph.SE_SUN,     "Sun"),
        (SwissEph.SE_MOON,    "Moon"),
        (SwissEph.SE_MARS,    "Mars"),
        (SwissEph.SE_MERCURY, "Mercury"),
        (SwissEph.SE_JUPITER, "Jupiter"),
        (SwissEph.SE_VENUS,   "Venus"),
        (SwissEph.SE_SATURN,  "Saturn"),
    };

    // Dignity per graha (sign index 0=Aries): exaltation, debilitation, own sign(s).
    private static readonly Dictionary<string, (int Exalt, int Debil, int[] Own)> Dignities = new()
    {
        ["Sun"]     = (0, 6,  new[] { 4 }),
        ["Moon"]    = (1, 7,  new[] { 3 }),
        ["Mars"]    = (9, 3,  new[] { 0, 7 }),
        ["Mercury"] = (5, 11, new[] { 2, 5 }),
        ["Jupiter"] = (3, 9,  new[] { 8, 11 }),
        ["Venus"]   = (11, 5, new[] { 1, 6 }),
        ["Saturn"]  = (6, 0,  new[] { 9, 10 }),
    };

    // Vimshottari dasha sequence: (lord, full period in years). Total = 120 years.
    private static readonly (string Lord, int Years)[] Vimshottari =
    {
        ("Ketu", 7), ("Venus", 20), ("Sun", 6), ("Moon", 10), ("Mars", 7),
        ("Rahu", 18), ("Jupiter", 16), ("Saturn", 19), ("Mercury", 17),
    };

    // Graha drishti (aspects) — every planet aspects the 7th; specials add more.
    private static readonly Dictionary<string, int[]> AspectHouses = new()
    {
        ["Sun"] = new[] { 7 }, ["Moon"] = new[] { 7 }, ["Mercury"] = new[] { 7 }, ["Venus"] = new[] { 7 },
        ["Mars"] = new[] { 4, 7, 8 }, ["Jupiter"] = new[] { 5, 7, 9 }, ["Saturn"] = new[] { 3, 7, 10 },
        ["Rahu"] = new[] { 5, 7, 9 }, ["Ketu"] = new[] { 5, 7, 9 },
    };

    // Deep-exaltation longitudes (for Uccha Bala). Debilitation point = +180°.
    private static readonly Dictionary<string, double> ExaltPoint = new()
    {
        ["Sun"] = 10, ["Moon"] = 33, ["Mars"] = 298, ["Mercury"] = 165, ["Jupiter"] = 95, ["Venus"] = 357, ["Saturn"] = 200,
    };

    // Naisargika (natural) bala in virupas, out of 60.
    private static readonly Dictionary<string, double> Naisargika = new()
    {
        ["Sun"] = 60.0, ["Moon"] = 51.43, ["Venus"] = 42.86, ["Jupiter"] = 34.29, ["Mercury"] = 25.71, ["Mars"] = 17.14, ["Saturn"] = 8.57,
    };

    // Dig Bala — ideal direction as an offset (°) from the Lagna: 1st(0), 4th(90),
    // 7th(180), 10th(270); the planet is powerless 180° away.
    private static readonly Dictionary<string, double> DigIdeal = new()
    {
        ["Jupiter"] = 0, ["Mercury"] = 0, ["Sun"] = 270, ["Mars"] = 270, ["Moon"] = 90, ["Venus"] = 90, ["Saturn"] = 180,
    };

    private static readonly int[] Kendras = { 1, 4, 7, 10 };
    // Sign lord (dispositor) by sign index 0=Aries … 11=Pisces.
    private static readonly string[] SignLord =
        { "Mars", "Venus", "Mercury", "Moon", "Sun", "Mercury", "Venus", "Mars", "Jupiter", "Saturn", "Saturn", "Jupiter" };

    // Life-area → primary house + natural significators (karakas).
    private static readonly (string Area, int House, string[] Karakas)[] AreaConfig =
    {
        ("love",      7,  new[] { "Venus" }),
        ("career",    10, new[] { "Sun", "Saturn", "Mercury" }),
        ("education", 5,  new[] { "Mercury", "Jupiter" }),
        ("social",    11, new[] { "Mercury", "Venus" }),
        ("health",    1,  new[] { "Sun", "Moon" }),
        ("wealth",    2,  new[] { "Jupiter" }),
    };

    public ApiResponse<BirthChartData> ComputeRasiChart(BirthChartRequest req)
    {
        // 1. Local birth time → UTC. IANA tz ids resolve historical DST on
        //    Linux/.NET 8 (Render). Wrong tz is the #1 source of chart errors.
        DateTime utc;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(req.TimeZone);
            var local = new DateTime(req.Year, req.Month, req.Day, req.Hour, req.Minute, req.Second, DateTimeKind.Unspecified);
            utc = TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }
        catch (Exception ex)
        {
            return ApiResponse<BirthChartData>.Fail($"Invalid date, time, or timezone: {ex.Message}", 400);
        }

        var swe = new SwissEph();
        try
        {
            // Sidereal zodiac, Lahiri ayanamsa.
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0);

            double hourUt = utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0;
            double jd = swe.swe_julday(utc.Year, utc.Month, utc.Day, hourUt, SwissEph.SE_GREG_CAL);

            // Moshier model → no external ephemeris files required.
            int iflag = SwissEph.SEFLG_MOSEPH | SwissEph.SEFLG_SIDEREAL | SwissEph.SEFLG_SPEED;

            // Ascendant / Lagna via Whole-Sign houses ('W').
            var cusps = new double[13];
            var ascmc = new double[10];
            swe.swe_houses_ex(jd, SwissEph.SEFLG_SIDEREAL, req.Latitude, req.Longitude, 'W', cusps, ascmc);
            double ascLon = Norm360(ascmc[0]);
            int ascSign = (int)(ascLon / 30.0);

            var planets = new List<PlanetPosition>();
            string serr = string.Empty;
            double moonLon = 0;

            foreach (var (id, name) in Grahas)
            {
                var xx = new double[6];
                int ret = swe.swe_calc_ut(jd, id, iflag, xx, ref serr);
                if (ret < 0)
                    return ApiResponse<BirthChartData>.Fail($"Ephemeris error for {name}: {serr}", 500);
                double plon = Norm360(xx[0]);
                if (name == "Moon") moonLon = plon;
                planets.Add(BuildPlanet(name, plon, xx[3] < 0, ascSign));
            }

            // Rahu (mean node) + Ketu (180° opposite). Nodes are always retrograde.
            var xr = new double[6];
            swe.swe_calc_ut(jd, SwissEph.SE_MEAN_NODE, iflag, xr, ref serr);
            double rahu = Norm360(xr[0]);
            planets.Add(BuildPlanet("Rahu", rahu, true, ascSign));
            planets.Add(BuildPlanet("Ketu", Norm360(rahu + 180.0), true, ascSign));

            // Second pass: drishti, then strength (both need the full planet set).
            FillAspects(planets, ascSign);
            FillStrength(planets, ascLon);
            var dashas = ComputeVimshottari(utc, moonLon);
            var maha = ActiveDasha(dashas);
            var antardashas = maha != null ? ComputeAntardashas(maha) : new List<DashaPeriod>();
            string mahaLord = maha?.Lord ?? "Sun";
            string bhuktiLord = ActiveDasha(antardashas)?.Lord ?? mahaLord;

            var data = new BirthChartData
            {
                Ascendant = BuildAscendant(ascLon),
                Planets = planets,
                Dashas = dashas,
                Antardashas = antardashas,
                Yogas = DetectYogas(planets),
                Predictions = ComputePredictions(planets, ascSign, mahaLord, bhuktiLord),
                Meta = new ChartMeta
                {
                    Ayanamsa = "Lahiri",
                    HouseSystem = "Whole Sign",
                    JulianDayUt = Math.Round(jd, 6),
                    UtcIso = utc.ToString("yyyy-MM-ddTHH:mm:ss'Z'"),
                    Latitude = req.Latitude,
                    Longitude = req.Longitude,
                },
            };
            return ApiResponse<BirthChartData>.Ok(data, "Chart computed.");
        }
        finally
        {
            swe.swe_close();
        }
    }

    private static PlanetPosition BuildPlanet(string name, double lon, bool retro, int ascSign)
    {
        int sign = (int)(lon / 30.0);
        double nakSize = 360.0 / 27.0;               // 13°20'
        int nak = (int)(lon / nakSize);
        int pada = (int)((lon - nak * nakSize) / (nakSize / 4.0)) + 1;
        int house = ((sign - ascSign + 12) % 12) + 1; // whole-sign
        int navamsa = VargaSign(lon, 9);
        return new PlanetPosition
        {
            Name = name,
            Longitude = Math.Round(lon, 4),
            Sign = sign,
            SignName = Signs[sign],
            SignNameSa = SignsSa[sign],
            DegreeInSign = Math.Round(lon - sign * 30.0, 4),
            Nakshatra = nak,
            NakshatraName = Nakshatras[nak],
            Pada = pada,
            House = house,
            Retrograde = retro,
            Dignity = DignityFor(name, sign),
            NavamsaSign = navamsa,
            NavamsaSignName = Signs[navamsa],
            Vargas = new Dictionary<string, int>
            {
                ["D2"] = VargaSign(lon, 2),
                ["D3"] = VargaSign(lon, 3),
                ["D7"] = VargaSign(lon, 7),
                ["D9"] = navamsa,
                ["D10"] = VargaSign(lon, 10),
                ["D12"] = VargaSign(lon, 12),
            },
        };
    }

    private static AscendantInfo BuildAscendant(double lon)
    {
        int sign = (int)(lon / 30.0);
        double nakSize = 360.0 / 27.0;
        int nak = (int)(lon / nakSize);
        int pada = (int)((lon - nak * nakSize) / (nakSize / 4.0)) + 1;
        int navamsa = VargaSign(lon, 9);
        return new AscendantInfo
        {
            Longitude = Math.Round(lon, 4),
            Sign = sign,
            SignName = Signs[sign],
            SignNameSa = SignsSa[sign],
            DegreeInSign = Math.Round(lon - sign * 30.0, 4),
            Nakshatra = nak,
            NakshatraName = Nakshatras[nak],
            Pada = pada,
            NavamsaSign = navamsa,
            NavamsaSignName = Signs[navamsa],
        };
    }

    private static string DignityFor(string name, int sign)
    {
        if (!Dignities.TryGetValue(name, out var d)) return "-";
        if (sign == d.Exalt) return "Exalted";
        if (sign == d.Debil) return "Debilitated";
        if (Array.IndexOf(d.Own, sign) >= 0) return "Own";
        return "-";
    }

    // Vimshottari mahadasha timeline from the Moon's nakshatra at birth. The first
    // period is partial (the BALANCE left of the ruling lord); the rest are full.
    private static List<DashaPeriod> ComputeVimshottari(DateTime birthUtc, double moonLon)
    {
        double nakSize = 360.0 / 27.0;
        int moonNak = (int)(moonLon / nakSize);
        double fracTraversed = (moonLon - moonNak * nakSize) / nakSize;   // 0–1 within the nakshatra
        int startIdx = moonNak % 9;

        var periods = new List<DashaPeriod>();
        var cursor = birthUtc;
        double firstYears = Vimshottari[startIdx].Years * (1.0 - fracTraversed);

        for (int i = 0; i <= 9; i++)   // starting partial + a full 9-lord cycle → covers a lifetime
        {
            var (lord, fullYears) = Vimshottari[(startIdx + i) % 9];
            double years = i == 0 ? firstYears : fullYears;
            var end = cursor.AddDays(years * 365.25);
            periods.Add(new DashaPeriod
            {
                Lord = lord,
                StartUtc = cursor.ToString("yyyy-MM-dd"),
                EndUtc = end.ToString("yyyy-MM-dd"),
                Years = Math.Round(years, 2),
            });
            cursor = end;
        }
        return periods;
    }

    // Divisional-chart (varga) sign for a sidereal longitude (Parashari rules).
    private static int VargaSign(double lon, int varga)
    {
        int rasi = (int)(lon / 30.0);
        double deg = lon - rasi * 30.0;
        bool oddSign = rasi % 2 == 0;   // Aries, Gemini, … are the 1st/3rd/… ("odd") signs
        switch (varga)
        {
            case 2:  // Hora — Leo(4)=Sun's hora, Cancer(3)=Moon's hora
                bool firstHalf = deg < 15.0;
                return oddSign ? (firstHalf ? 4 : 3) : (firstHalf ? 3 : 4);
            case 3:  // Drekkana → same / 5th / 9th
                return (rasi + (int)(deg / 10.0) * 4) % 12;
            case 7:  // Saptamsa → odd sign: same, even sign: 7th
                return ((oddSign ? rasi : (rasi + 6) % 12) + (int)(deg / (30.0 / 7.0))) % 12;
            case 9:  // Navamsa (continuous 3°20' division)
                return (int)(lon / (30.0 / 9.0)) % 12;
            case 10: // Dasamsa → odd sign: same, even sign: 9th
                return ((oddSign ? rasi : (rasi + 8) % 12) + (int)(deg / 3.0)) % 12;
            case 12: // Dwadasamsa → same, + part
                return (rasi + (int)(deg / 2.5)) % 12;
            default:
                return rasi;
        }
    }

    // Graha drishti: fill each planet's aspected houses (1–12) + aspected planets.
    private static void FillAspects(List<PlanetPosition> planets, int ascSign)
    {
        foreach (var p in planets)
        {
            var houses = AspectHouses.TryGetValue(p.Name, out var h) ? h : new[] { 7 };
            var aspectedSigns = houses.Select(x => (p.Sign + x - 1) % 12).ToHashSet();
            p.AspectsHouses = aspectedSigns.Select(s => ((s - ascSign + 12) % 12) + 1).OrderBy(x => x).ToArray();
            p.AspectsPlanets = planets.Where(q => q.Name != p.Name && aspectedSigns.Contains(q.Sign)).Select(q => q.Name).ToArray();
        }
    }

    // Extended Shadbala (5 components): Sthana(Uccha) + Dig + Naisargika + Paksha
    // (Kala) + Drik (aspectual). A fully-validated Parashari Shadbala also needs the
    // remaining Kala sub-parts (Nathonnata/Ayana/…), Cheshta and Yuddha bala —
    // a later phase. Rahu/Ketu are not part of classical Shadbala → null.
    private static void FillStrength(List<PlanetPosition> planets, double ascLon)
    {
        double sunLon = planets.First(p => p.Name == "Sun").Longitude;
        double moonLon = planets.First(p => p.Name == "Moon").Longitude;
        double elong = Math.Abs(moonLon - sunLon); if (elong > 180) elong = 360 - elong;   // 0–180
        bool moonWaxing = ((moonLon - sunLon + 360.0) % 360.0) < 180.0;

        foreach (var p in planets)
        {
            if (!Naisargika.ContainsKey(p.Name)) { p.Strength = null; continue; }

            double debil = (ExaltPoint[p.Name] + 180.0) % 360.0;
            double du = Math.Abs(p.Longitude - debil); if (du > 180) du = 360 - du;
            double uccha = du / 3.0;

            double ideal = (ascLon + DigIdeal[p.Name]) % 360.0;
            double powerless = (ideal + 180.0) % 360.0;
            double dd = Math.Abs(p.Longitude - powerless); if (dd > 180) dd = 360 - dd;
            double dig = dd / 3.0;

            double nais = Naisargika[p.Name];

            // Paksha bala: benefics gain on the waxing Moon, malefics on the waning.
            double pakshaBase = elong / 180.0 * 60.0;
            double paksha = IsBenefic(p.Name, moonWaxing) ? pakshaBase : 60.0 - pakshaBase;

            // Drik bala: net (benefic − malefic) aspects received, scaled + clamped.
            int ben = planets.Count(q => q.Name != p.Name && q.AspectsPlanets.Contains(p.Name) && IsBenefic(q.Name, moonWaxing));
            int mal = planets.Count(q => q.Name != p.Name && q.AspectsPlanets.Contains(p.Name) && !IsBenefic(q.Name, moonWaxing));
            double drik = Math.Clamp((ben - mal) * 15.0, -60.0, 60.0);

            double total = uccha + dig + nais + paksha + drik;
            p.Strength = new PlanetStrength
            {
                UcchaBala = Math.Round(uccha, 2),
                DigBala = Math.Round(dig, 2),
                NaisargikaBala = Math.Round(nais, 2),
                PakshaBala = Math.Round(paksha, 2),
                DrikBala = Math.Round(drik, 2),
                TotalVirupas = Math.Round(total, 2),
                TotalRupas = Math.Round(total / 60.0, 2),
            };
        }
    }

    // Benefics: Jupiter, Venus, Mercury, and the waxing (bright) Moon.
    private static bool IsBenefic(string name, bool moonWaxing) =>
        name is "Jupiter" or "Venus" or "Mercury" || (name == "Moon" && moonWaxing);

    // Classic yogas from sign/house placements (whole-sign).
    private static List<Yoga> DetectYogas(List<PlanetPosition> planets)
    {
        var by = planets.ToDictionary(p => p.Name);
        int Sign(string n) => by[n].Sign;
        int HouseFrom(int planetSign, int refSign) => ((planetSign - refSign + 12) % 12) + 1;
        var yogas = new List<Yoga>();

        if (Kendras.Contains(HouseFrom(Sign("Jupiter"), Sign("Moon"))))
            yogas.Add(new Yoga { Name = "Gaja Kesari Yoga", Description = "Jupiter in a kendra (1/4/7/10) from the Moon — wisdom, virtue, prosperity.", Planets = new[] { "Jupiter", "Moon" } });

        if (Sign("Sun") == Sign("Mercury"))
            yogas.Add(new Yoga { Name = "Budha-Aditya Yoga", Description = "Sun and Mercury conjunct — intellect, communication, learning.", Planets = new[] { "Sun", "Mercury" } });

        if (Sign("Moon") == Sign("Mars"))
            yogas.Add(new Yoga { Name = "Chandra-Mangala Yoga", Description = "Moon and Mars conjunct — drive and wealth through enterprise.", Planets = new[] { "Moon", "Mars" } });

        void Mahapurusha(string planet, string yoga)
        {
            var d = by[planet];
            if (d.Dignity is "Own" or "Exalted" && Kendras.Contains(d.House))
                yogas.Add(new Yoga { Name = yoga + " Yoga", Description = $"{planet} in own/exaltation in a kendra — a Pancha Mahapurusha yoga.", Planets = new[] { planet } });
        }
        Mahapurusha("Mars", "Ruchaka");
        Mahapurusha("Mercury", "Bhadra");
        Mahapurusha("Jupiter", "Hamsa");
        Mahapurusha("Venus", "Malavya");
        Mahapurusha("Saturn", "Sasa");

        foreach (var p in planets.Where(p => p.Dignity == "Debilitated"))
        {
            string lord = SignLord[p.Sign];
            if (by.TryGetValue(lord, out var l) && Kendras.Contains(l.House))
                yogas.Add(new Yoga { Name = "Neecha Bhanga Raja Yoga", Description = $"{p.Name} is debilitated but its dispositor {lord} sits in a kendra — debilitation cancelled.", Planets = new[] { p.Name, lord } });
        }

        return yogas;
    }

    private static DashaPeriod? ActiveDasha(List<DashaPeriod> periods)
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return periods.FirstOrDefault(d => string.CompareOrdinal(d.StartUtc, today) <= 0 && string.CompareOrdinal(today, d.EndUtc) < 0)
               ?? periods.FirstOrDefault();
    }

    // Antardasha (bhukti) sub-periods within a mahadasha — proportional to its span,
    // in Vimshottari order starting from the mahadasha lord.
    private static List<DashaPeriod> ComputeAntardashas(DashaPeriod maha)
    {
        int start = Array.FindIndex(Vimshottari, v => v.Lord == maha.Lord);
        if (start < 0) start = 0;
        DateTime s = DateTime.Parse(maha.StartUtc), e = DateTime.Parse(maha.EndUtc);
        double totalDays = (e - s).TotalDays;
        var list = new List<DashaPeriod>();
        var cursor = s;
        for (int i = 0; i < 9; i++)
        {
            var (lord, yrs) = Vimshottari[(start + i) % 9];
            double days = totalDays * yrs / 120.0;
            var end = cursor.AddDays(days);
            list.Add(new DashaPeriod { Lord = lord, StartUtc = cursor.ToString("yyyy-MM-dd"), EndUtc = end.ToString("yyyy-MM-dd"), Years = Math.Round(days / 365.25, 2) });
            cursor = end;
        }
        return list;
    }

    private static readonly int[] Upachaya = { 1, 4, 5, 7, 9, 10 };   // kendra + trikona (strong)
    private static readonly int[] Dusthana = { 6, 8, 12 };            // difficult houses

    // Rule-based per-area predictions: house-lord dignity + placement, karaka
    // dignity, occupants, aspects (drishti) and dasha/bhukti activation. Emits
    // STRUCTURED findings; the frontend localizes them to EN / မြန်မာ sentences.
    private static List<AreaPrediction> ComputePredictions(List<PlanetPosition> planets, int ascSign, string mahaLord, string bhuktiLord)
    {
        var by = planets.ToDictionary(p => p.Name);
        double sunLon = by["Sun"].Longitude, moonLon = by["Moon"].Longitude;
        bool moonWaxing = ((moonLon - sunLon + 360.0) % 360.0) < 180.0;
        var result = new List<AreaPrediction>();

        foreach (var (area, house, karakas) in AreaConfig)
        {
            int score = 50;
            var findings = new List<Finding>();
            string lord = SignLord[(ascSign + house - 1) % 12];

            // 1. House-lord dignity.
            string lordDig = by[lord].Dignity;
            score += lordDig switch { "Exalted" => 20, "Own" => 12, "Debilitated" => -20, _ => 0 };
            findings.Add(new Finding { Code = "lordDignity", Planet = lord, House = house, Value = lordDig });

            // 2. House-lord placement (which house the lord occupies).
            int lordHouse = by[lord].House;
            if (Upachaya.Contains(lordHouse)) score += 8;
            else if (Dusthana.Contains(lordHouse)) score -= 10;
            findings.Add(new Finding { Code = "lordPlacement", Planet = lord, House = lordHouse, Value = Dusthana.Contains(lordHouse) ? "dusthana" : Upachaya.Contains(lordHouse) ? "strong" : "neutral" });

            // 3. Karaka (significator) dignities.
            foreach (var k in karakas)
            {
                string kd = by[k].Dignity;
                if (kd is "Exalted" or "Own" or "Debilitated")
                {
                    score += kd switch { "Exalted" => 12, "Own" => 6, "Debilitated" => -12, _ => 0 };
                    findings.Add(new Finding { Code = "karakaDignity", Planet = k, Value = kd });
                }
            }

            // 4. Occupants of the house.
            foreach (var o in planets.Where(p => p.House == house))
            {
                bool ben = IsBenefic(o.Name, moonWaxing);
                score += ben ? 8 : -8;
                findings.Add(new Finding { Code = "occupant", Planet = o.Name, House = house, Value = ben ? "benefic" : "malefic" });
            }

            // 5. Aspects on the house (graha drishti).
            foreach (var q in planets.Where(p => p.AspectsHouses.Contains(house)))
            {
                bool ben = IsBenefic(q.Name, moonWaxing);
                score += ben ? 6 : -6;
                findings.Add(new Finding { Code = "aspectOnHouse", Planet = q.Name, House = house, Value = ben ? "benefic" : "malefic" });
            }

            // 6. Dasha / bhukti activation.
            if (lord == mahaLord || karakas.Contains(mahaLord))
            {
                score += 10;
                findings.Add(new Finding { Code = "dashaActive", Planet = mahaLord, Value = area });
            }
            if (bhuktiLord != mahaLord && (lord == bhuktiLord || karakas.Contains(bhuktiLord)))
            {
                score += 8;
                findings.Add(new Finding { Code = "bhuktiActive", Planet = bhuktiLord, Value = area });
            }

            score = Math.Clamp(score, 0, 100);
            string tone = score >= 65 ? "favorable" : score <= 40 ? "testing" : "mixed";
            result.Add(new AreaPrediction { Area = area, Tone = tone, Score = score, Findings = findings });
        }
        return result;
    }

    private static double Norm360(double x) => ((x % 360.0) + 360.0) % 360.0;
}
