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

    [Fact] // regola che matcha vento+pioggia prevale e dà DEP/ARR
    public void Rules_Match_Wind_And_Precip()
    {
        var rules = new[]
        {
            new RunwayRuleEval(130, 200, null, null, true, null, "16R", "16L", "vento da sud, pioggia"),
            new RunwayRuleEval(null, null, null, null, null, null, "34L", "34R", "default"),
        };
        var hit = RunwaySuggestion.EvaluateRules(rules, 160, 12, rain: true, snow: false);
        Assert.NotNull(hit);
        Assert.Equal("16R", hit!.Dep);
        Assert.Equal("16L", hit.Arr);

        // pioggia assente → la prima regola non matcha, vince il default.
        var dflt = RunwaySuggestion.EvaluateRules(rules, 160, 12, rain: false, snow: false);
        Assert.Equal("34L", dflt!.Dep);
    }

    [Fact] // arco vento con wrap-around (350→020)
    public void Rules_Wind_Arc_Wraps()
    {
        var rules = new[] { new RunwayRuleEval(350, 20, null, null, null, null, "01", "01", null) };
        Assert.NotNull(RunwaySuggestion.EvaluateRules(rules, 10, 8, false, false));
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 180, 8, false, false));
    }

    [Fact] // nessuna regola applicabile → null (il viewer userà il fallback headwind)
    public void Rules_No_Match_Returns_Null()
    {
        var rules = new[] { new RunwayRuleEval(130, 200, null, null, null, null, "16", "16", null) };
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 10, 8, false, false));
    }

    [Fact] // finestra oraria UTC con wrap notturno (22:00→06:00Z)
    public void Rules_Time_Window_Wraps()
    {
        var rules = new[] { new RunwayRuleEval(null, null, null, null, null, null, "35", "35", "notte",
            TimeFromUtcMin: 22 * 60, TimeToUtcMin: 6 * 60) };
        // dentro finestra (23:00Z) → matcha; fuori (12:00Z) → no.
        Assert.NotNull(RunwaySuggestion.EvaluateRules(rules, 350, 10, false, false, new DateTime(2026, 6, 22, 23, 0, 0, DateTimeKind.Utc)));
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 350, 10, false, false, new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact] // vincolo giorno della settimana (solo lunedì = bit0)
    public void Rules_Day_Of_Week()
    {
        var rules = new[] { new RunwayRuleEval(null, null, null, null, null, null, "35", "35", null, DaysOfWeekMask: 1) };
        Assert.NotNull(RunwaySuggestion.EvaluateRules(rules, 350, 10, false, false, new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc))); // lunedì
        Assert.Null(RunwaySuggestion.EvaluateRules(rules, 350, 10, false, false, new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc)));    // martedì
    }

    [Fact] // parità giorno del mese (alternanza tipo Malpensa)
    public void Rules_Date_Parity()
    {
        var even = new[] { new RunwayRuleEval(null, null, null, null, null, null, "35", "35", null, DateParity: DateParity.Even) };
        Assert.NotNull(RunwaySuggestion.EvaluateRules(even, 350, 10, false, false, new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc))); // 22 pari
        Assert.Null(RunwaySuggestion.EvaluateRules(even, 350, 10, false, false, new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc)));    // 23 dispari
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
