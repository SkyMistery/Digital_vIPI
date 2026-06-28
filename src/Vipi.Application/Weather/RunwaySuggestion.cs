using System.Text.RegularExpressions;
using Vipi.Domain;

namespace Vipi.Application.Weather;

/// <summary>Esito suggerimento pista dal vento: estremità migliore + componenti (kt) + nota leggibile.</summary>
public sealed record RunwayPick(string Ident, int Heading, int Headwind, int Crosswind);

/// <summary>
/// Esito calcolo headwind. <see cref="DepIdent"/>/<see cref="ArrIdent"/> distinguono le estremità su piste
/// parallele nella stessa direzione del vento (es. 35L arrivi / 35R partenze); coincidono con <see cref="Best"/>
/// quando non ci sono parallele utili.
/// </summary>
public sealed record RunwaySuggestionResult(RunwayPick? Best, IReadOnlyList<RunwayPick> Ranked, string Note,
    string? DepIdent = null, string? ArrIdent = null);

/// <summary>Regola di scelta pista (DTO disaccoppiato dalle entità): condizione vento/precip/tempo → piste DEP/ARR.</summary>
public sealed record RunwayRuleEval(int? WindDirFrom, int? WindDirTo, int? WindSpeedMin, int? WindSpeedMax,
    bool? Rain, bool? Snow, string DepRunways, string ArrRunways, string? Note,
    int? TimeFromUtcMin = null, int? TimeToUtcMin = null, int? DaysOfWeekMask = null, DateParity DateParity = DateParity.Any);

/// <summary>Esito di una regola applicata: piste DEP/ARR + nota.</summary>
public sealed record RunwayRuleResult(string Dep, string Arr, string? Note);

/// <summary>
/// Sceglie la pista col massimo componente di testa-vento. Le estremità arrivano come ident ("16L","07","34R").
/// Vento calmo/non noto → nessun suggerimento (nota esplicita).
/// </summary>
public static partial class RunwaySuggestion
{
    [GeneratedRegex(@"^(\d{1,2})([LRC]?)$")]
    private static partial Regex IdentRe();

    public static RunwaySuggestionResult Suggest(IEnumerable<string> runwayIdents, int? windDir, int windKt)
    {
        var ends = runwayIdents
            .Select(i => (Ident: i.Trim().ToUpperInvariant(), M: IdentRe().Match(i.Trim())))
            .Where(x => x.M.Success)
            .Select(x => (x.Ident, Heading: int.Parse(x.M.Groups[1].Value) * 10))
            .ToList();

        if (ends.Count == 0)
            return new RunwaySuggestionResult(null, Array.Empty<RunwayPick>(), "Nessuna pista nota.");

        if (windDir is null || windKt <= 2)
            return new RunwaySuggestionResult(null, Array.Empty<RunwayPick>(),
                windKt <= 2 ? "Vento calmo: pista a discrezione." : "Direzione vento non disponibile.");

        var ranked = ends
            .Select(e =>
            {
                var diff = AngleDiff(windDir.Value, e.Heading);
                var rad = diff * Math.PI / 180.0;
                var head = (int)Math.Round(windKt * Math.Cos(rad));
                var cross = (int)Math.Round(Math.Abs(windKt * Math.Sin(rad)));
                return new RunwayPick(e.Ident, e.Heading, head, cross);
            })
            .OrderByDescending(p => p.Headwind)
            .ThenBy(p => p.Crosswind)
            .ToList();

        var best = ranked[0];
        // Piste parallele nella direzione del vento (stesso heading): split arrivi/partenze (sinistra=arrivi, destra=partenze).
        var parallels = ranked.Where(p => p.Heading == best.Heading).OrderBy(p => p.Ident, StringComparer.Ordinal).ToList();
        var (depIdent, arrIdent) = parallels.Count >= 2
            ? (parallels[^1].Ident, parallels[0].Ident)   // ARR = prima (es. 35L), DEP = ultima (es. 35R)
            : (best.Ident, best.Ident);

        var note = best.Headwind < 0
            ? $"Attenzione: vento in coda su {best.Ident} ({-best.Headwind} kt). Nessuna pista favorevole."
            : $"Testa-vento {best.Headwind} kt su {best.Ident}" +
              (best.Crosswind > 0 ? $", traverso {best.Crosswind} kt" : "") +
              (parallels.Count >= 2 ? $". Arrivi {arrIdent}, partenze {depIdent}." : ".");

        return new RunwaySuggestionResult(best, ranked, note, depIdent, arrIdent);
    }

    /// <summary>
    /// Prima regola applicabile alle condizioni correnti → piste DEP/ARR (prevale sul calcolo headwind).
    /// null = nessuna regola matcha (il chiamante usa <see cref="Suggest"/> come fallback).
    /// </summary>
    public static RunwayRuleResult? EvaluateRules(IEnumerable<RunwayRuleEval> rules, int? windDir, int windKt, bool rain, bool snow,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var minOfDay = now.Hour * 60 + now.Minute;
        foreach (var r in rules)
        {
            if (!WindDirInArc(r.WindDirFrom, r.WindDirTo, windDir)) continue;
            if (r.WindSpeedMin is int mn && windKt < mn) continue;
            if (r.WindSpeedMax is int mx && windKt > mx) continue;
            if (r.Rain is bool rr && rr != rain) continue;
            if (r.Snow is bool ss && ss != snow) continue;
            if (!TimeInWindow(r.TimeFromUtcMin, r.TimeToUtcMin, minOfDay)) continue;
            if (!DayOfWeekMatches(r.DaysOfWeekMask, now)) continue;
            if (!ParityMatches(r.DateParity, now)) continue;

            var dep = string.IsNullOrWhiteSpace(r.DepRunways) ? r.ArrRunways : r.DepRunways;
            var arr = string.IsNullOrWhiteSpace(r.ArrRunways) ? r.DepRunways : r.ArrRunways;
            return new RunwayRuleResult(dep.Trim(), arr.Trim(), string.IsNullOrWhiteSpace(r.Note) ? null : r.Note!.Trim());
        }
        return null;
    }

    /// <summary>Vero se la direzione vento ricade nell'arco [from,to] (gestisce il wrap, es. 350→010). Estremi null = nessun vincolo.</summary>
    private static bool WindDirInArc(int? from, int? to, int? windDir)
    {
        if (from is null && to is null) return true;       // regola senza vincolo di direzione
        if (windDir is not int d) return false;            // vincolo presente ma direzione non nota
        var f = from ?? 0;
        var t = to ?? 360;
        return f <= t ? d >= f && d <= t : d >= f || d <= t;
    }

    /// <summary>Vero se l'orario (minuti UTC) ricade nella finestra [from,to] (gestisce il wrap notturno, es. 22:00→06:00). Estremi null = nessun vincolo.</summary>
    private static bool TimeInWindow(int? from, int? to, int minOfDay)
    {
        if (from is null && to is null) return true;
        var f = from ?? 0;
        var t = to ?? 1439;
        return f <= t ? minOfDay >= f && minOfDay <= t : minOfDay >= f || minOfDay <= t;
    }

    /// <summary>Vero se il giorno corrente è nel bitmask (bit0=Lun … bit6=Dom). null/0 = tutti i giorni.</summary>
    private static bool DayOfWeekMatches(int? mask, DateTime now)
    {
        if (mask is not int m || m == 0) return true;
        var bit = ((int)now.DayOfWeek + 6) % 7;            // .NET: Dom=0 → rimappa a Lun=0..Dom=6
        return (m & (1 << bit)) != 0;
    }

    /// <summary>Vero se la parità del giorno del mese soddisfa il vincolo. Any = sempre vero.</summary>
    private static bool ParityMatches(DateParity parity, DateTime now) => parity switch
    {
        DateParity.Even => now.Day % 2 == 0,
        DateParity.Odd => now.Day % 2 != 0,
        _ => true,
    };

    /// <summary>Differenza angolare minima con segno non rilevante per il coseno; ritorna -180..180.</summary>
    private static int AngleDiff(int a, int b)
    {
        var d = (a - b) % 360;
        if (d > 180) d -= 360;
        if (d < -180) d += 360;
        return d;
    }
}
