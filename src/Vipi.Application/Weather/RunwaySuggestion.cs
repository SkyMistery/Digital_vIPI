using System.Text.RegularExpressions;
using Vipi.Domain;

namespace Vipi.Application.Weather;

/// <summary>Esito suggerimento pista dal vento: estremità migliore + componenti (kt).</summary>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record RunwayPick(string Ident, int Heading, int Headwind, int Crosswind);

/// <summary>
/// Esito calcolo headwind. <see cref="DepIdent"/>/<see cref="ArrIdent"/> distinguono le estremità su piste
/// parallele nella stessa direzione del vento (es. 35L arrivi / 35R partenze); coincidono con <see cref="Best"/>
/// quando non ci sono parallele utili.
/// </summary>
public sealed record RunwaySuggestionResult(RunwayPick? Best, IReadOnlyList<RunwayPick> Ranked,
    SuggestionReason Reason, string? DepIdent = null, string? ArrIdent = null);

/// <summary>
/// Com'è andato il ripiego sul vento: con una pista scelta (<see cref="Headwind"/>, <see cref="Tailwind"/>) o
/// senza, e perché.
///
/// <para>⚠️ <b>Un codice e non una frase</b> (16 settembre 2026). Fino ad allora il risultato portava una
/// <c>Note</c> in italiano cablato — «Headwind 8 kt su 16, vento traverso 6 kt», «Vento calmo: pista a
/// discrezione.» — e il banco di prova dell'editor la stampava così com'era anche nell'interfaccia inglese.
/// L'Application non conosce la lingua di chi legge: la frase la scrive la UI dalle risorse, e le componenti
/// in kt stanno già in <see cref="RunwaySuggestionResult.Best"/>.</para>
/// </summary>
public enum SuggestionReason
{
    /// <summary>Scelta la pista col massimo headwind.</summary>
    Headwind,

    /// <summary>Scelta la «migliore», ma il vento è in coda su tutte: nessuna pista favorevole.</summary>
    Tailwind,

    /// <summary>Nessuna pista riconoscibile fra quelle date.</summary>
    NoRunways,

    /// <summary>Vento calmo (≤ 2 kt): pista a discrezione.</summary>
    Calm,

    /// <summary>Vento senza direzione (variabile o non noto).</summary>
    NoDirection,

    /// <summary>
    /// Le piste ci sono, ma sono <b>tutte</b> escluse sia in partenza sia in arrivo: il ripiego non ha niente fra cui
    /// scegliere. ⚠️ Distinto da <see cref="NoRunways"/>: lì la tabella è vuota, qui la tabella c'è e l'ha svuotata una
    /// scelta.
    /// </summary>
    AllNeverUse,
}

/// <summary>
/// Le soglie che il ripiego sul vento non sceglie mai, per verso (carta 2026-09-17-pista-mai-usare.md): una soglia
/// può essere esclusa solo dalle partenze, solo dagli arrivi, o da tutti e due.
/// </summary>
public sealed record RunwayExclusions(IReadOnlyCollection<string> Departures, IReadOnlyCollection<string> Arrivals)
{
    public static RunwayExclusions None { get; } = new(Array.Empty<string>(), Array.Empty<string>());
}

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
/// Perché una regola si applica o no, nell'ordine in cui il motore controlla: il PRIMO vincolo che non passa.
/// <see cref="Applies"/> = passano tutti (la regola vince se nessuna prima di lei si applica).
/// </summary>
public enum RuleVerdict { Applies, Surface, Time, Day, Parity, Season, Tailwind, Crosswind }

/// <summary>Il vento proiettato su una pista di una regola: tailwind (0 se il vento arriva di fronte) e vento traverso, in kt.</summary>
public sealed record RunwayWindComponents(string Ident, int TailwindKt, int CrosswindKt);

/// <summary>
/// Una regola passata al motore, con tutto quel che serve a capirla: le componenti su OGNI sua pista, le peggiori
/// (quelle confrontate con le soglie) e l'esito.
/// </summary>
public sealed record RuleExplanation(int RuleIndex, RuleVerdict Verdict,
    IReadOnlyList<RunwayWindComponents> Runways, int WorstTailwindKt, int WorstCrosswindKt);

/// <summary>
/// Sceglie la pista col massimo headwind. Le estremità arrivano come ident ("16L","07","34R").
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

    /// <summary>
    /// L'ora locale italiana di un istante UTC — la <b>stessa</b> conversione che fa <see cref="EvaluateRules"/>.
    ///
    /// <para>⚠️ Esiste perché il banco di prova dell'editor deve mostrare e leggere un'ora locale, e una seconda
    /// conversione scritta lassù direbbe una cosa mentre il motore ne calcola un'altra: il fuso è uno solo, e la
    /// porta per attraversarlo è questa.</para>
    /// </summary>
    public static DateTime OraLocale(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ItalyTimeZone);

    /// <summary>
    /// L'istante UTC di un'ora locale italiana. Inverso di <see cref="OraLocale"/>.
    ///
    /// <para>⚠️ L'ora che <b>non esiste</b> non alza un'eccezione: nel salto di primavera le 02:30 non esistono, e
    /// un banco di prova che esplodesse su una data battuta a mano sarebbe peggio di uno che prova le 03:30.</para>
    /// </summary>
    public static DateTime AUtc(DateTime locale)
    {
        var l = DateTime.SpecifyKind(locale, DateTimeKind.Unspecified);
        if (ItalyTimeZone.IsInvalidTime(l)) l = l.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(l, ItalyTimeZone);
    }

    /// <param name="exclusions">
    /// Le soglie che il ripiego non sceglie mai, per verso (carta 2026-09-17-pista-mai-usare.md).
    /// <para>⚠️ <b>La regola sta QUI, e solo qui</b>: il ripiego si calcola in cinque posti (documenti, vAWOS, vista
    /// rapida, elenco aeroporti, banco di prova) e ognuno passa soltanto il dato. Filtrare nei chiamanti vorrebbe
    /// dire cinque copie, e il giorno che una manca il vAWOS direbbe una pista e il documento un'altra.</para>
    /// <para>⚠️ <see cref="RunwaySuggestionResult.DepIdent"/> e <see cref="RunwaySuggestionResult.ArrIdent"/> si
    /// scelgono <b>ciascuno fra le soglie ammesse in quel verso</b>, e possono essere null (tutte escluse in quel
    /// verso). I chiamanti NON devono ripiegare su <see cref="RunwaySuggestionResult.Best"/>: sarebbe proporre per
    /// gli arrivi una soglia marcata «mai in arrivo».</para>
    /// </param>
    public static RunwaySuggestionResult Suggest(IEnumerable<string> runwayIdents, int? windDir, int windKt,
        RunwayExclusions? exclusions = null)
    {
        static HashSet<string> Insieme(IEnumerable<string>? v) =>
            new((v ?? Array.Empty<string>()).Select(i => (i ?? "").Trim()), StringComparer.OrdinalIgnoreCase);
        var noDep = Insieme(exclusions?.Departures);
        var noArr = Insieme(exclusions?.Arrivals);
        var tutte = runwayIdents
            .Select(i => (Ident: i.Trim().ToUpperInvariant(), M: IdentRe().Match(i.Trim())))
            .Where(x => x.M.Success)
            .Select(x => (x.Ident, Heading: int.Parse(x.M.Groups[1].Value) * 10))
            .ToList();
        // Fuori dal ripiego solo le soglie escluse in TUTTI E DUE i versi: le altre servono almeno a uno.
        var ends = tutte.Where(e => !(noDep.Contains(e.Ident) && noArr.Contains(e.Ident))).ToList();

        if (ends.Count == 0)
            return new RunwaySuggestionResult(null, Array.Empty<RunwayPick>(),
                tutte.Count > 0 ? SuggestionReason.AllNeverUse : SuggestionReason.NoRunways);

        if (windDir is null || windKt <= 2)
            return new RunwaySuggestionResult(null, Array.Empty<RunwayPick>(),
                windKt <= 2 ? SuggestionReason.Calm : SuggestionReason.NoDirection);

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
        // Ogni verso sceglie fra le SUE soglie ammesse. Piste parallele nella direzione del vento (stesso heading):
        // split arrivi/partenze (sinistra = arrivi, destra = partenze). Senza esclusioni i due insiemi coincidono con
        // `ranked` e l'esito è quello di sempre.
        static string? Scegli(IReadOnlyList<RunwayPick> ammesse, bool arrivi)
        {
            if (ammesse.Count == 0) return null;
            var parallele = ammesse.Where(p => p.Heading == ammesse[0].Heading)
                .OrderBy(p => p.Ident, StringComparer.Ordinal).ToList();
            return parallele.Count >= 2
                ? (arrivi ? parallele[0].Ident : parallele[^1].Ident)   // ARR = prima (es. 35L), DEP = ultima (es. 35R)
                : ammesse[0].Ident;
        }
        var depIdent = Scegli(ranked.Where(p => !noDep.Contains(p.Ident)).ToList(), arrivi: false);
        var arrIdent = Scegli(ranked.Where(p => !noArr.Contains(p.Ident)).ToList(), arrivi: true);

        var motivo = best.Headwind < 0 ? SuggestionReason.Tailwind : SuggestionReason.Headwind;
        return new RunwaySuggestionResult(best, ranked, motivo, depIdent, arrIdent);
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
        var vincente = ExplainRules(rules, windDir, windKt, wet, nowUtc).FirstOrDefault(e => e.Verdict == RuleVerdict.Applies);
        if (vincente is null) return null;

        var r = rules[vincente.RuleIndex];
        var dep = string.IsNullOrWhiteSpace(r.DepRunways) ? r.ArrRunways : r.DepRunways;
        var arr = string.IsNullOrWhiteSpace(r.ArrRunways) ? r.DepRunways : r.ArrRunways;
        return new RunwayRuleResult(dep.Trim(), arr.Trim(),
            string.IsNullOrWhiteSpace(r.Note) ? null : r.Note!.Trim(), vincente.RuleIndex,
            string.IsNullOrWhiteSpace(r.Name) ? null : r.Name!.Trim());
    }

    /// <summary>
    /// Ogni regola, e perché si applica o no: le componenti del vento su ciascuna delle sue piste e il primo
    /// vincolo che non passa.
    ///
    /// <para>⚠️ È IL motore, non una sua copia per il banco di prova dell'editor: <see cref="EvaluateRules"/> è
    /// «la prima di queste che si applica». Una spiegazione scritta a parte potrebbe dire «si applica» su una
    /// regola che il motore scarta, ed è la cosa peggiore che un banco di prova possa fare.</para>
    ///
    /// <para>Le componenti si calcolano SEMPRE, anche quando la regola cade prima (superficie, orario): chi prova
    /// vuole vedere i numeri comunque. Vento calmo (≤ 2 kt) o senza direzione: zero, come nel confronto.</para>
    /// </summary>
    public static IReadOnlyList<RuleExplanation> ExplainRules(IReadOnlyList<RunwayRuleEval> rules, int? windDir,
        int windKt, bool wet, DateTime? nowUtc = null)
    {
        // Orari/giorni/stagione AIP sono in ora LOCALE: porto l'istante UTC all'ora locale italiana prima dei confronti.
        var utc = DateTime.SpecifyKind(nowUtc ?? DateTime.UtcNow, DateTimeKind.Utc);
        var now = TimeZoneInfo.ConvertTimeFromUtc(utc, ItalyTimeZone);
        var minOfDay = now.Hour * 60 + now.Minute;
        var esiti = new List<RuleExplanation>(rules.Count);
        for (var i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            var piste = Components(r, windDir, windKt);
            var tail = piste.Count == 0 ? 0 : piste.Max(p => p.TailwindKt);
            var cross = piste.Count == 0 ? 0 : piste.Max(p => p.CrosswindKt);
            // ⚠️ Il giorno da confrontare è quello OPERATIVO, non quello del calendario: vedi GiornoOperativo.
            var giorno = GiornoOperativo(now, minOfDay, r.TimeFromLocalMin, r.TimeToLocalMin);

            var verdetto =
                !SurfaceMatches(r.Surface, wet) ? RuleVerdict.Surface
                : !TimeInWindow(r.TimeFromLocalMin, r.TimeToLocalMin, minOfDay) ? RuleVerdict.Time
                : !DayOfWeekMatches(r.DaysOfWeekMask, giorno) ? RuleVerdict.Day
                : !ParityMatches(r.DateParity, giorno) ? RuleVerdict.Parity
                : !DateInWindow(r.DateFromMonthDay, r.DateToMonthDay, giorno) ? RuleVerdict.Season
                : tail > r.MaxTailwindKt ? RuleVerdict.Tailwind
                : r.MaxCrosswindKt is int mc && cross > mc ? RuleVerdict.Crosswind
                : RuleVerdict.Applies;

            esiti.Add(new RuleExplanation(i, verdetto, piste, tail, cross));
        }
        return esiti;
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

    /// <summary>
    /// Tailwind e vento traverso su OGNI pista della regola (DEP poi ARR, ogni ident una volta). Le soglie si
    /// confrontano coi PEGGIORI di questi. Vento calmo/ignoto → zero su tutte.
    /// <para>⚠️ Il tailwind è <c>max(0, -headwind)</c>: un vento di fronte non è «tailwind negativo». Il confronto
    /// con la soglia non cambia (la soglia è ≥ 0), ma a schermo un «-8 kt» in colonna tailwind si leggeva male.</para>
    /// </summary>
    private static List<RunwayWindComponents> Components(RunwayRuleEval r, int? windDir, int windKt)
    {
        var piste = Idents(r.DepRunways).Concat(Idents(r.ArrRunways))
            .DistinctBy(p => p.Ident, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return piste.Select(p =>
        {
            if (windDir is not int wd || windKt <= 2) return new RunwayWindComponents(p.Ident, 0, 0);
            var rad = AngleDiff(wd, p.Heading) * Math.PI / 180.0;
            var head = (int)Math.Round(windKt * Math.Cos(rad));
            var cross = (int)Math.Round(Math.Abs(windKt * Math.Sin(rad)));
            return new RunwayWindComponents(p.Ident, Math.Max(0, -head), cross);
        }).ToList();
    }

    /// <summary>Ident e heading (gradi) delle estremità in un CSV di ident (es. "16L,16R" → [(16L,160),(16R,160)]).</summary>
    private static IEnumerable<(string Ident, int Heading)> Idents(string? csv) => (csv ?? "")
        .Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(i => (Ident: i.ToUpperInvariant(), M: IdentRe().Match(i)))
        .Where(x => x.M.Success)
        .Select(x => (x.Ident, int.Parse(x.M.Groups[1].Value) * 10));

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
