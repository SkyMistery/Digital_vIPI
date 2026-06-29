using Vipi.Application.Weather;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Decoder METAR/TAF + suggerimento pista dal vento (vista vIPI aeroporto).</summary>
public class WeatherParsingTests
{
    [Fact] // METAR campione mockup LIRF
    public void Metar_Parses_All_Fields()
    {
        var m = MetarParser.ParseMetar("LIRF 191250Z 16012KT 9999 FEW035 SCT100 26/14 Q1015 NOSIG");

        Assert.Equal("LIRF", m.Station);
        Assert.Equal("191250Z", m.TimeRaw);
        Assert.NotNull(m.Wind);
        Assert.Equal(160, m.Wind!.DirectionDeg);
        Assert.Equal(12, m.Wind.SpeedKt);
        Assert.Equal("160° / 12 kt", m.Wind.Label);
        Assert.Equal(">10 km", m.Visibility);
        Assert.Equal(2, m.Clouds.Count);
        Assert.Equal("FEW 3500", m.Clouds[0].Label);
        Assert.Equal("SCT 10000", m.Clouds[1].Label);
        Assert.Equal(1015, m.QnhHpa);
        Assert.Equal(26, m.TempC);
        Assert.Equal(14, m.DewpointC);
        Assert.Equal("NOSIG", m.Trend);
    }

    [Fact] // gust + raffica, negativi, CAVOK, weather
    public void Metar_Handles_Gust_Negatives_Cavok_Weather()
    {
        var m = MetarParser.ParseMetar("LIML 010620Z 20015G25KT CAVOK M03/M05 Q0998 -RA");
        Assert.Equal(15, m.Wind!.SpeedKt);
        Assert.Equal(25, m.Wind.GustKt);
        Assert.Equal(">10 km", m.Visibility);
        Assert.Equal(-3, m.TempC);
        Assert.Equal(-5, m.DewpointC);
        Assert.Equal(998, m.QnhHpa);
        Assert.Contains("pioggia", m.Weather);
    }

    [Fact] // vento calmo + visibilità in metri
    public void Metar_Calm_And_Meters_Visibility()
    {
        var m = MetarParser.ParseMetar("LIRA 010000Z 00000KT 4000 BR 10/09 Q1020");
        Assert.True(m.Wind!.Calm);
        Assert.Equal("Calmo", m.Wind.Label);
        Assert.Equal("4000 m", m.Visibility);
        Assert.Contains("foschia", m.Weather);
    }

    [Fact] // inHg → hPa
    public void Metar_Altimeter_InHg_Converts()
    {
        var m = MetarParser.ParseMetar("KJFK 011200Z 30010KT 10SM FEW250 15/05 A2992");
        Assert.Equal(1013, m.QnhHpa);
    }

    [Fact] // TAF: base + BECMG + TEMPO + BECMG
    public void Taf_Splits_Segments()
    {
        var taf = MetarParser.ParseTaf(
            "LIRF 191100Z 1912/2018 16012KT 9999 FEW035 SCT100 BECMG 1918/1920 20015G25KT TEMPO 2000/2006 4000 RA BKN012 BECMG 2008/2010 24008KT");

        Assert.Equal("LIRF", taf.Station);
        Assert.Equal("1912/2018", taf.ValidityRaw);
        Assert.Equal(4, taf.Segments.Count);

        Assert.Equal(TafChangeKind.Base, taf.Segments[0].Kind);
        Assert.Equal(160, taf.Segments[0].Wind!.DirectionDeg);

        Assert.Equal(TafChangeKind.Becmg, taf.Segments[1].Kind);
        Assert.Equal("1918/1920", taf.Segments[1].PeriodRaw);
        Assert.Equal(25, taf.Segments[1].Wind!.GustKt);

        Assert.Equal(TafChangeKind.Tempo, taf.Segments[2].Kind);
        Assert.Equal("4000 m", taf.Segments[2].Visibility);
        Assert.Contains("pioggia", taf.Segments[2].Weather);

        Assert.Equal(TafChangeKind.Becmg, taf.Segments[3].Kind);
        Assert.Equal(240, taf.Segments[3].Wind!.DirectionDeg);
    }

    [Fact] // pista: vento 160/12 favorisce 16
    public void Runway_Picks_Best_Headwind()
    {
        var r = RunwaySuggestion.Suggest(new[] { "16L", "16R", "34L", "34R", "07", "25" }, 160, 12);
        Assert.NotNull(r.Best);
        Assert.StartsWith("16", r.Best!.Ident);
        Assert.True(r.Best.Headwind > 10);
        Assert.True(r.Best.Crosswind <= 1);
    }

    [Fact] // pista calma → nessun suggerimento
    public void Runway_Calm_No_Suggestion()
    {
        var r = RunwaySuggestion.Suggest(new[] { "16L", "34R" }, null, 0);
        Assert.Null(r.Best);
        Assert.NotEmpty(r.Note);
    }

    [Fact] // vento in coda su tutte → nota di attenzione
    public void Runway_Tailwind_Warns()
    {
        var r = RunwaySuggestion.Suggest(new[] { "16" }, 340, 20);
        Assert.True(r.Best!.Headwind < 0);
        Assert.Contains("coda", r.Note);
    }

    [Fact] // pioggia e neve riconosciute dai codici
    public void Metar_Detects_Rain_And_Snow()
    {
        Assert.True(MetarParser.ParseMetar("LIRF 191250Z 16012KT 9999 -SHRA SCT020 10/08 Q1010").HasRain);
        Assert.False(MetarParser.ParseMetar("LIRF 191250Z 16012KT 9999 -SHRA SCT020 10/08 Q1010").HasSnow);
        var sn = MetarParser.ParseMetar("LIMC 010620Z 02008KT 2000 SN OVC008 M02/M03 Q1005");
        Assert.True(sn.HasSnow);
        Assert.False(sn.HasRain);
    }

    // --- Regole pista (EvaluateRules): scelta in base a coda/traverso + superficie, prima regola che si applica ---

    private static RunwayRuleEval Rule(string dep, string arr, int maxTail = 5, int? maxCross = null,
        RunwaySurface surface = RunwaySurface.Any, string? name = null, DateParity parity = DateParity.Any) =>
        new(dep, arr, name, null, maxTail, maxCross, surface, null, null, null, parity);

    [Fact] // la regola si applica se il vento in coda è entro la soglia; altrimenti null (fallback)
    public void Rule_Fires_Within_Tailwind_Limit()
    {
        var rules = new[] { Rule("16", "16", maxTail: 5) };
        // vento 160/12 = testa su 16 (coda negativa) → si applica.
        var hit = RunwaySuggestion.EvaluateRules(rules, 160, 12, wet: false);
        Assert.NotNull(hit);
        Assert.Equal("16", hit!.Arr);
        // vento 340/12 = coda 12 kt > 5 → non si applica.
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 340, 12, wet: false));
    }

    [Fact] // prima regola applicabile per ordine → l'altra pista quando la prima è in coda
    public void Rule_Picks_First_Applicable_By_Order()
    {
        var rules = new[] { Rule("16", "16"), Rule("34", "34") };
        Assert.Equal(0, RunwaySuggestion.EvaluateRules(rules, 160, 12, false)!.RuleIndex);   // 16 in testa
        var sw = RunwaySuggestion.EvaluateRules(rules, 340, 12, false);                       // 16 in coda → 34
        Assert.Equal(1, sw!.RuleIndex);
        Assert.Equal("34", sw.Arr);
    }

    [Fact] // limite di traverso: oltre soglia la regola non si applica
    public void Rule_Crosswind_Limit_Gates()
    {
        var rules = new[] { Rule("16", "16", maxTail: 5, maxCross: 10) };
        // vento 250 perpendicolare a 16: coda 0 ma traverso = velocità piena.
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 250, 15, false));     // traverso 15 > 10 → no
        Assert.NotNull(RunwaySuggestion.EvaluateRules(rules, 250, 8, false));   // traverso 8 ≤ 10 → sì
    }

    [Fact] // condizione superficie: asciutta vs bagnata
    public void Rule_Surface_Dry_Or_Wet()
    {
        var rules = new[]
        {
            Rule("16", "16", surface: RunwaySurface.Dry, name: "asciutta"),
            Rule("34", "34", surface: RunwaySurface.Wet, name: "bagnata"),
        };
        // calmo: coda 0 su entrambe ⇒ decide la superficie.
        Assert.Equal("16", RunwaySuggestion.EvaluateRules(rules, null, 0, wet: false)!.Arr);
        Assert.Equal("34", RunwaySuggestion.EvaluateRules(rules, null, 0, wet: true)!.Arr);
    }

    [Fact] // LIMC: parallele segregate, DEP≠ARR preservati
    public void Rule_LIMC_Segregated_Parallels()
    {
        var rules = new[] { Rule("35R", "35L", name: "35"), Rule("17L", "17R", name: "17") };
        var r = RunwaySuggestion.EvaluateRules(rules, 350, 15, false);   // vento da 350 → 35
        Assert.Equal(0, r!.RuleIndex);
        Assert.Equal("35R", r.Dep);
        Assert.Equal("35L", r.Arr);
        Assert.NotEqual(r.Dep, r.Arr);
    }

    [Fact] // vento calmo → si applica la prima regola (coda 0)
    public void Rule_Calm_Applies_First()
    {
        var rules = new[] { Rule("16", "16"), Rule("34", "34") };
        Assert.Equal(0, RunwaySuggestion.EvaluateRules(rules, null, 0, false)!.RuleIndex);
        Assert.Equal(0, RunwaySuggestion.EvaluateRules(rules, 340, 1, false)!.RuleIndex);   // ≤ 2 kt
    }

    [Fact] // nessuna regola si applica → null (il viewer usa il fallback headwind Suggest)
    public void Rule_No_Match_Returns_Null()
    {
        var rules = new[] { Rule("16", "16", maxTail: 5) };
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 340, 20, false));   // coda 20 > 5
        Assert.Null(RunwaySuggestion.EvaluateRules(Array.Empty<RunwayRuleEval>(), 160, 12, false));
    }

    [Fact] // filtro avanzato (parità): la prima non eleggibile → si applica la successiva
    public void Rule_Advanced_Parity_Skips()
    {
        var rules = new[]
        {
            Rule("16", "16", name: "solo pari", parity: DateParity.Even),
            Rule("34", "34", name: "default"),
        };
        // 23 giugno 2026 = dispari → la #1 (solo pari) è esclusa, si applica la #2.
        var r = RunwaySuggestion.EvaluateRules(rules, null, 0, false, new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(1, r!.RuleIndex);
        Assert.Equal("34", r.Arr);
    }

    [Fact] // finestra stagionale ricorrente (MMDD): dentro al periodo si applica, fuori no
    public void Rule_Seasonal_Date_Window_Filters_By_Month_Day()
    {
        // valida solo dal 1 gen (101) al 31 mar (331); l'anno è ignorato.
        var rules = new[]
        {
            new RunwayRuleEval("16", "16", "inverno", null, 5, null, RunwaySurface.Any,
                DateFromMonthDay: 101, DateToMonthDay: 331),
        };
        Assert.NotNull(RunwaySuggestion.EvaluateRules(rules, 160, 12, false, new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 160, 12, false, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact] // finestra stagionale a cavallo di fine anno (es. 1 nov → 28 feb)
    public void Rule_Seasonal_Date_Window_Wraps_Year_End()
    {
        var rules = new[]
        {
            new RunwayRuleEval("16", "16", "stagione fredda", null, 5, null, RunwaySurface.Any,
                DateFromMonthDay: 1101, DateToMonthDay: 228),
        };
        Assert.NotNull(RunwaySuggestion.EvaluateRules(rules, 160, 12, false, new DateTime(2026, 12, 20, 12, 0, 0, DateTimeKind.Utc)));
        Assert.NotNull(RunwaySuggestion.EvaluateRules(rules, 160, 12, false, new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 160, 12, false, new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact] // fallback headwind: piste parallele → arrivi/partenze su estremità distinte
    public void Suggest_Splits_Parallel_Dep_Arr()
    {
        var s = RunwaySuggestion.Suggest(new[] { "35L", "35R" }, 350, 15);
        Assert.NotNull(s.Best);
        Assert.Equal("35L", s.ArrIdent);   // sinistra = arrivi
        Assert.Equal("35R", s.DepIdent);   // destra = partenze
        Assert.NotEqual(s.DepIdent, s.ArrIdent);
    }

    [Fact] // pista singola: DEP e ARR coincidono (nessuna parallela)
    public void Suggest_Single_Runway_Same_End()
    {
        var s = RunwaySuggestion.Suggest(new[] { "34", "16" }, 340, 12);
        Assert.Equal(s.DepIdent, s.ArrIdent);
        Assert.Equal("34", s.DepIdent);
    }
}
