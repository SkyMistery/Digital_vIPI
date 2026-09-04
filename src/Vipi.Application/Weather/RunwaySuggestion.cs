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

/// <summary>
/// Regola di scelta pista (DTO disaccoppiato dalle entità): piste DEP/ARR preferenziali + soglie operative
/// (coda/traverso massimi, superficie) + filtro temporale opzionale. Tailwind/crosswind sono calcolati dal vento.
/// </summary>
public sealed record RunwayRuleEval(string DepRunways, string ArrRunways, string? Name, string? Note,
    int MaxTailwindKt, int? MaxCrosswindKt, RunwaySurface Surface,
    int? TimeFromLocalMin = null, int? TimeToLocalMin = null, int? DaysOfWeekMask = null, DateParity DateParity = DateParity.Any,
    int? DateFromMonthDay = null, int? DateToMonthDay = null);

/// <summary>Esito: piste DEP/ARR della regola che si applica + nota + indice/nome della regola.</summary>
public sealed record RunwayRuleResult(string Dep, string Arr, string? Note, int RuleIndex = 0, string? RuleName = null);

/// <summary>
/// Sceglie la pista col massimo componente di testa-vento. Le estremità arrivano come ident ("16L","07","34R").
/// Vento calmo/non noto → nessun suggerimento (nota esplicita).
/// </summary>
public static partial class RunwaySuggestion
{
    [GeneratedRegex(@"^(\d{1,2})([LRC]?)$")]
    private static partial Regex IdentRe();

    /// <summary>Fuso orario italiano (CET/CEST con DST) per interpretare gli orari AIP in ora locale. Risolto cross-platform.</summary>
    private static readonly TimeZoneInfo ItalyTimeZone = ResolveItalyTimeZone();

    private static TimeZoneInfo ResolveItalyTimeZone()
    {
        foreach (var id in new[] { "Europe/Rome", "W. Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;   // fallback estremo: nessun fuso disponibile
    }

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
    /// Prima regola applicabile alle condizioni correnti → piste DEP/ARR preferenziali. Una regola si applica se,
    /// sulle sue piste, il vento in coda è ≤ <c>MaxTailwindKt</c> e il traverso ≤ <c>MaxCrosswindKt</c> (se valorizzato)
    /// e la superficie corrisponde (<paramref name="wet"/> = pioggia/neve) e l'eventuale filtro temporale è soddisfatto.
    /// null = nessuna regola si applica (il chiamante usa <see cref="Suggest"/> come fallback — "altrimenti l'altra").
    ///
    /// <para><paramref name="nowUtc"/> è l'istante da cui si ricavano ora, giorno e stagione: null = adesso. Lo
    /// passa il banco di prova dell'editor, che serve a provare una regola in un momento che non è questo.</para>
    /// </summary>
    public static RunwayRuleResult? EvaluateRules(IReadOnlyList<RunwayRuleEval> rules, int? windDir, int windKt, bool wet,
        DateTime? nowUtc = null)
    {
        // Orari/giorni/stagione AIP sono in ora LOCALE: porto l'istante UTC all'ora locale italiana prima dei confronti.
        var utc = DateTime.SpecifyKind(nowUtc ?? DateTime.UtcNow, DateTimeKind.Utc);
        var now = TimeZoneInfo.ConvertTimeFromUtc(utc, ItalyTimeZone);
        var minOfDay = now.Hour * 60 + now.Minute;
        for (var i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (!SurfaceMatches(r.Surface, wet)) continue;
            if (!TimeInWindow(r.TimeFromLocalMin, r.TimeToLocalMin, minOfDay)) continue;
            // ⚠️ Il giorno da confrontare è quello OPERATIVO, non quello del calendario: vedi GiornoOperativo.
            var giorno = GiornoOperativo(now, minOfDay, r.TimeFromLocalMin, r.TimeToLocalMin);
            if (!DayOfWeekMatches(r.DaysOfWeekMask, giorno)) continue;
            if (!ParityMatches(r.DateParity, giorno)) continue;
            if (!DateInWindow(r.DateFromMonthDay, r.DateToMonthDay, giorno)) continue;

            var (tail, cross) = WorstComponents(r, windDir, windKt);
            if (tail > r.MaxTailwindKt) continue;
            if (r.MaxCrosswindKt is int mc && cross > mc) continue;

            var dep = string.IsNullOrWhiteSpace(r.DepRunways) ? r.ArrRunways : r.DepRunways;
            var arr = string.IsNullOrWhiteSpace(r.ArrRunways) ? r.DepRunways : r.ArrRunways;
            return new RunwayRuleResult(dep.Trim(), arr.Trim(),
                string.IsNullOrWhiteSpace(r.Note) ? null : r.Note!.Trim(), i,
                string.IsNullOrWhiteSpace(r.Name) ? null : r.Name!.Trim());
        }
        return null;
    }

    /// <summary>
    /// Il <b>giorno operativo</b> di una regola: quello a cui appartiene la finestra oraria in corso, che dopo
    /// mezzanotte <b>non</b> è il giorno del calendario.
    ///
    /// <para>Una finestra che scavalca — «lunedì 22:00→05:00» — dura fino alle cinque di <b>martedì</b>, e chi
    /// la scrive intende un solo turno di notte, non due monconi. Confrontando il giorno del calendario la
    /// regola si spegneva a mezzanotte: alle 03:00 il giorno è martedì, la maschera dice lunedì, e la regola
    /// smetteva di applicarsi a metà della notte che descrive. Misurato il 4 settembre 2026.</para>
    ///
    /// <para>⚠️ Lo scalino vale per <b>tutti e tre</b> i filtri di calendario — giorno della settimana, parità
    /// e finestra stagionale — e non per il solo giorno della settimana: «le notti dei giorni dispari» e «le
    /// notti di novembre» si spezzerebbero a mezzanotte nello stesso identico modo.</para>
    ///
    /// <para>Senza scavalco (o senza finestra oraria) il giorno operativo <b>è</b> quello del calendario:
    /// nessuna regola già scritta cambia significato.</para>
    /// </summary>
    private static DateTime GiornoOperativo(DateTime now, int minOfDay, int? from, int? to)
    {
        if (from is not int f || to is not int t || f <= t) return now;   // nessuno scavalco: giorno di calendario
        return minOfDay <= t ? now.AddDays(-1) : now;                     // coda dopo mezzanotte → il giorno prima
    }

    private static bool SurfaceMatches(RunwaySurface s, bool wet) => s switch
    {
        RunwaySurface.Dry => !wet,
        RunwaySurface.Wet => wet,
        _ => true,
    };

    /// <summary>Vento in coda e al traverso PEGGIORI (massimi) sulle piste della regola (DEP∪ARR). Vento calmo/ignoto/nessuna pista → (0,0).</summary>
    private static (int Tail, int Cross) WorstComponents(RunwayRuleEval r, int? windDir, int windKt)
    {
        if (windDir is not int wd || windKt <= 2) return (0, 0);
        var headings = Idents(r.ArrRunways).Concat(Idents(r.DepRunways)).ToList();
        if (headings.Count == 0) return (0, 0);
        int tail = int.MinValue, cross = 0;
        foreach (var heading in headings)
        {
            var rad = AngleDiff(wd, heading) * Math.PI / 180.0;
            var head = (int)Math.Round(windKt * Math.Cos(rad));
            var c = (int)Math.Round(Math.Abs(windKt * Math.Sin(rad)));
            if (-head > tail) tail = -head;       // tailwind = -headwind; tieni il peggiore
            if (c > cross) cross = c;
        }
        return (tail, cross);
    }

    /// <summary>Heading (gradi) delle estremità in un CSV di ident (es. "16L,16R" → [160,160]).</summary>
    private static List<int> Idents(string? csv) => (csv ?? "")
        .Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(i => IdentRe().Match(i))
        .Where(m => m.Success)
        .Select(m => int.Parse(m.Groups[1].Value) * 10)
        .ToList();

    /// <summary>Vero se l'orario (minuti locali) ricade nella finestra [from,to] (gestisce il wrap notturno, es. 22:00→06:00). Estremi null = nessun vincolo.</summary>
    private static bool TimeInWindow(int? from, int? to, int minOfDay)
    {
        if (from is null && to is null) return true;
        var f = from ?? 0;
        var t = to ?? 1439;
        return f <= t ? minOfDay >= f && minOfDay <= t : minOfDay >= f || minOfDay <= t;
    }

    /// <summary>Vero se il giorno OPERATIVO (<see cref="GiornoOperativo"/>) è nel bitmask (bit0=Lun … bit6=Dom).
    /// null/0 = tutti i giorni.</summary>
    private static bool DayOfWeekMatches(int? mask, DateTime giorno)
    {
        if (mask is not int m || m == 0) return true;
        var bit = ((int)giorno.DayOfWeek + 6) % 7;         // .NET: Dom=0 → rimappa a Lun=0..Dom=6
        return (m & (1 << bit)) != 0;
    }

    /// <summary>Vero se la data del giorno OPERATIVO (mese/giorno) ricade nella finestra stagionale ricorrente
    /// [from,to] in MMDD (estremi inclusi, gestisce il wrap di fine anno, es. 1101→0228). Entrambi null = nessun
    /// vincolo.</summary>
    private static bool DateInWindow(int? from, int? to, DateTime giorno)
    {
        if (from is null && to is null) return true;
        var md = giorno.Month * 100 + giorno.Day;
        var f = from ?? 101;        // default: inizio anno
        var t = to ?? 1231;         // default: fine anno
        return f <= t ? md >= f && md <= t : md >= f || md <= t;
    }

    /// <summary>Vero se la parità del giorno OPERATIVO del mese soddisfa il vincolo. Any = sempre vero.</summary>
    private static bool ParityMatches(DateParity parity, DateTime giorno) => parity switch
    {
        DateParity.Even => giorno.Day % 2 == 0,
        DateParity.Odd => giorno.Day % 2 != 0,
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
