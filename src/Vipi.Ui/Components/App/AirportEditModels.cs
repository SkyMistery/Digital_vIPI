using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Domain;

namespace Vipi.Ui.Components.App;

// I modelli di SCRITTURA delle sezioni dell'aeroporto (doc 14 §3g).
//
// ⚠️ Perché esistono accanto alle viste di lettura (`AirportTlRowView`, `AirportRunwayRowView`, …), e perché
// NON sono un difetto: le due forme sono davvero diverse. La lettura è una proiezione già formattata — la
// fascia QNH è la stringa «1014 – 1030», l'initial climb è una quota o un livello a seconda della TA — perché
// dev'essere serializzabile per il congelamento della release. La scrittura ha i campi separati e mutabili,
// perché è ciò che un `<input>` sa legare.
//
// È per questo che qui NON si applica il modello «un componente, due modi» che vale per AppSeparations o
// AppFrequencies: là lettura e scrittura sono la stessa riga. Il difetto vero, e quello che questo giro
// chiude, era un altro — questi editor stavano scritti DENTRO la pagina, 523 righe di marcatura che nessun
// test poteva montare.
//
// Erano classi private annidate in AeroportoEditorPage: pubbliche qui perché i componenti d'editor, che
// stanno fuori dalla pagina, devono poterle ricevere come parametro.

/// <summary>Riga in scrittura della tabella dei livelli di transizione: la fascia QNH ha estremi separati
/// (<c>From</c>/<c>To</c>, null = aperta), che in lettura diventano una stringa sola.</summary>
public sealed class TlEdit { public int Id; public int? From; public int? To; public string? Level; }

/// <summary>Riga in scrittura.</summary>
public sealed class RwEdit { public int Id; public string? Ident; public int? LengthM; public int? Bearing; public string? Tora; public string? Lda; public string? App; public string? Patterns; public string? Circling; }
/// <summary>Riga in scrittura.</summary>
public sealed class RuleEdit
{
    public string? Name;
    public HashSet<string> Dep = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Arr = new(StringComparer.OrdinalIgnoreCase);
    public int MaxTail = 5;                                    // vento in coda massimo (kt)
    public int? MaxCross;                                      // vento al traverso massimo (kt); null = nessun vincolo
    public string Surface = "any";                             // "any"|"dry"|"wet"
    public string? Note;
    // ⚠️ LOCALE, non UTC: gli orari AIP si scrivono in ora italiana (migrazione RenameRunwayRuleTimeToLocal).
    // Se TimeTo <= TimeFrom la finestra scavalca la mezzanotte e appartiene al giorno in cui è cominciata.
    public TimeOnly? TimeFrom; public TimeOnly? TimeTo;        // finestra oraria locale (avanzate)
    public int DaysMask;                                       // bit0=Lun … bit6=Dom; 0 = tutti (avanzate)
    public string Parity = "";                                 // ""|"even"|"odd" (avanzate)
    public int? DateFromDay; public int? DateFromMonth;        // finestra stagionale ricorrente: giorno+mese, nessun anno (avanzate)
    public int? DateToDay; public int? DateToMonth;
}
    // Stessi campi editoriali delle importate: la quota «da concordare con l'APP» e la priorità fra le SID
    // dello stesso punto esistono già sull'entità (una tabella sola per manuali e importate), erano solo
    // scoperte dall'editor.
/// <summary>Riga in scrittura.</summary>
public sealed class SidEdit
{
    public string? Runway; public string? Fix; public string? Name; public string? Transition;
    public string? InitialClimb; public bool InitialClimbByApp; public string? Type;
    public string? Cat; public string? Wtc; public string? Condition; public int? Priority;
    // Stato di pubblicazione calcolato al caricamento con la regola del dominio (SidRow.IsPublicAt).
    public bool IsPublicNow = true; public string PublishFromCycle = "—";
}
/// <summary>Riga in scrittura.</summary>
public sealed class ImportedSidEdit
{
    public int Id; public string Fix = ""; public string Name = ""; public string? Runway; public string? Transition; public string? Type;
    public int? Priority; public bool ForcePublished; public bool NeedsReview; public bool IsPublicNow; public string PublishFromCycle = "—";
    public string? FixOverride; public bool CreateAlias;
    // Arricchimenti editoriali sovrapposti alla riga di sorgente (persistiti con la riga, preservati al reimport).
    public string? InitialClimb; public bool InitialClimbByApp; public string? Cat; public string? Wtc; public string? Condition;
}


/// <summary>Un problema trovato in una tabella d'editor, in forma da tradurre: la chiave di
/// risorsa e i suoi argomenti. ⚠️ Separato dal testo apposta — così la regola si può provare senza montare un
/// localizzatore, ed è la parte che vale la pena provare.</summary>
// ⚠️ Pubblico per FORZA: compare in un `[Parameter]` di un componente Razor, e la classe che Razor
// genera è pubblica. Un tipo che sta nella firma di un componente è superficie del modulo quanto il
// componente stesso (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record AirportTlIssue(string Key, object[] Args);

/// <summary>
/// Le regole della tabella dei livelli di transizione. È il <b>cuore deterministico</b> dell'editor: nessun
/// I/O, nessuna UI, solo righe che entrano e problemi che escono — l'invariante #8 del runbook di refactor
/// chiede esattamente questo prima di spezzare un componente.
/// </summary>
public static class AirportTlValidation
{
    /// <summary>Righe senza livello, estremi invertiti, fasce che si accavallano.</summary>
    public static IReadOnlyList<AirportTlIssue> Issues(IReadOnlyList<TlEdit> rows)
    {
        var w = new List<AirportTlIssue>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(rows[i].Level))
                w.Add(new AirportTlIssue("Ape_IssueTlMissing", new object[] { i + 1 }));

            // Estremo assente = fascia APERTA da quel lato: «fino a 1013» e «da 1014 in su» si scrivono così.
            var aLo = rows[i].From ?? int.MinValue;
            var aHi = rows[i].To ?? int.MaxValue;
            if (aLo > aHi) w.Add(new AirportTlIssue("Ape_IssueQnhOrder", new object[] { i + 1 }));

            for (var j = i + 1; j < rows.Count; j++)
            {
                var bLo = rows[j].From ?? int.MinValue;
                var bHi = rows[j].To ?? int.MaxValue;
                if (aLo <= bHi && bLo <= aHi)
                    w.Add(new AirportTlIssue("Ape_IssueQnhOverlap", new object[] { i + 1, j + 1 }));
            }
        }
        return w;
    }
}

/// <summary>Le regole della tabella piste. Cuore deterministico, come <see cref="AirportTlValidation"/>.</summary>
public static class AirportRunwayValidation
{
    /// <summary>
    /// Identificativi ripetuti. ⚠️ Le righe SENZA identificativo si saltano e non sono un errore: sono la
    /// riga appena aggiunta, che non si è ancora finito di scrivere — segnalarla vorrebbe dire mostrare un
    /// avviso rosso a chi ha appena premuto «+ Pista».
    /// </summary>
    public static IReadOnlyList<AirportTlIssue> Issues(IReadOnlyList<RwEdit> rows)
    {
        var w = new List<AirportTlIssue>();
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var id = (r.Ident ?? "").Trim();
            if (id.Length == 0) continue;
            if (!visti.Add(id)) w.Add(new AirportTlIssue("Ape_IssueRwDup", new object[] { id }));
        }
        return w;
    }
}

/// <summary>
/// Le tre colonne a scelta della tabella piste — procedure d'avvicinamento, circuiti, circling — scritte a chip
/// dall'11 settembre 2026. Prima erano testo libero, e il testo libero aveva prodotto «RNAV» accanto a «RNP», una
/// tabulazione in testa a una cella e «N» per dire «niente circling».
///
/// <para>⚠️ <b>Lo storage NON cambia</b>: resta la stringa separata da virgole di prima (<c>"ILS, LOC, VOR"</c>).
/// È quel che rende il cambio innocuo per i documenti già pubblicati — le release fotografano la stringa
/// (<see cref="AirportRunwayRowView"/>), e la lettura continua a scriverla così com'è. Niente migrazione.</para>
///
/// <para>⚠️ <b>Quel che non sta nell'elenco non si butta</b>: un valore scritto col vecchio sistema resta nel campo
/// e l'editor lo mostra come chip «fuori elenco» con la sua ✕. Toglierlo è un gesto di chi redige, mai un effetto
/// collaterale del clic su un altro chip — altrimenti il primo clic su «ILS» cancellerebbe in silenzio l'«RNAV»
/// che qualcuno aveva scritto, e il documento pubblicato successivo lo perderebbe senza che nessuno l'abbia
/// deciso.</para>
///
/// <para>Cuore deterministico: stringa che entra, stringa che esce. Nessuna UI.</para>
/// </summary>
public static class RunwayChoices
{
    /// <summary>Procedure d'avvicinamento, nell'ordine in cui si scrivono.</summary>
    public static readonly IReadOnlyList<string> App =
        new[] { "ILS", "LOC", "RNP", "VOR", "NDB", "TAC", "HTAC", "PAR", "SRA" };

    /// <summary>Circuiti di traffico.</summary>
    public static readonly IReadOnlyList<string> Patterns = new[] { "L", "R", "L JET", "R JET" };

    /// <summary>Lati del circling. ⚠️ Nessun chip = circling non ammesso: la cella si legge «—».</summary>
    public static readonly IReadOnlyList<string> Circling = new[] { "L", "R" };

    /// <summary>Un campo letto contro il suo elenco: le voci riconosciute e quelle che l'elenco non ha.</summary>
    /// <param name="Known">Le voci dell'elenco presenti, scritte come le scrive l'elenco.</param>
    /// <param name="Legacy">Le voci fuori elenco, nell'ordine in cui compaiono nel campo.</param>
    public sealed record Parsed(IReadOnlyList<string> Known, IReadOnlyList<string> Legacy);

    /// <summary>
    /// Divide il campo con la regola della lettura (<see cref="AirportRunwayLists.Tokens"/>: virgole e punti e
    /// virgola, spazi ripetuti ridotti) e confronta ogni pezzo con l'elenco senza badare alle maiuscole: «l  jet»
    /// è «L JET». ⚠️ Non si divide sugli spazi: «L JET» è UNA voce.
    /// </summary>
    public static Parsed Parse(string? value, IReadOnlyList<string> choices)
    {
        var known = new List<string>();
        var legacy = new List<string>();
        foreach (var tok in AirportRunwayLists.Tokens(value))
        {
            var hit = choices.FirstOrDefault(c => string.Equals(c, tok, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) { if (!known.Contains(hit)) known.Add(hit); }
            else if (!legacy.Contains(tok, StringComparer.OrdinalIgnoreCase)) legacy.Add(tok);
        }
        return new Parsed(known, legacy);
    }

    /// <summary>
    /// Accende o spegne una voce dell'elenco. Le voci si riscrivono nell'ordine dell'ELENCO, non in quello del
    /// clic — così due piste con le stesse procedure si leggono uguali — e quelle fuori elenco restano in coda.
    /// Campo vuoto = <c>null</c>, come prima delle chip.
    /// </summary>
    public static string? Toggle(string? value, IReadOnlyList<string> choices, string choice)
    {
        var p = Parse(value, choices);
        var on = p.Known.ToHashSet();
        if (!on.Remove(choice)) on.Add(choice);
        return Compose(choices.Where(on.Contains), p.Legacy);
    }

    /// <summary>Toglie una voce fuori elenco: è l'unico modo in cui una voce vecchia lascia il campo.</summary>
    public static string? RemoveLegacy(string? value, IReadOnlyList<string> choices, string legacy)
    {
        var p = Parse(value, choices);
        return Compose(choices.Where(p.Known.Contains),
            p.Legacy.Where(l => !string.Equals(l, legacy, StringComparison.OrdinalIgnoreCase)));
    }

    private static string? Compose(IEnumerable<string> known, IEnumerable<string> legacy)
    {
        var s = string.Join(", ", known.Concat(legacy));
        return s.Length == 0 ? null : s;
    }
}

/// <summary>
/// Il picker delle frequenze collegabili: filtro e nomi delle posizioni. Cuore deterministico, provabile
/// senza montare niente.
/// </summary>
public static class AirportFrequencyPicker
{
    /// <summary>
    /// Le frequenze che corrispondono a quel che si sta scrivendo — callsign, frequenza o ICAO — al più 50.
    /// ⚠️ Il tetto non è cosmetico: il catalogo collegabile è l'intera divisione, e senza si disegnerebbero
    /// centinaia di righe dentro un menù a tendina a ogni tasto premuto.
    /// </summary>
    public static IEnumerable<LinkableFrequencyRow> Filtra(IEnumerable<LinkableFrequencyRow> tutte, string? cerca)
    {
        var q = (cerca ?? "").Trim();
        return tutte.Where(f =>
            q.Length == 0
            || f.Callsign.Contains(q, StringComparison.OrdinalIgnoreCase)
            || f.FrequencyMhz.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (f.Icao?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(50);
    }

    /// <summary>Il nome per esteso di una posizione ATC. Sconosciuta = si scrive com'è arrivata.</summary>
    public static string NomePosizione(string? position) => (position ?? "").Trim().ToUpperInvariant() switch
    {
        "ATIS" => "ATIS",
        "DEL" => "Delivery",
        "GND" => "Ground",
        "TWR" => "Tower",
        "APP" => "Approach",
        "DEP" => "Departure",
        "CTR" => "Control",
        "FSS" => "Information",
        _ => position ?? "—",
    };
}

/// <summary>Esito della validazione delle regole piste: gli errori impediscono il salvataggio, gli avvisi no.</summary>
internal sealed record AirportRuleIssues(
    IReadOnlyList<AirportTlIssue> Errors, IReadOnlyList<AirportTlIssue> Warnings);

/// <summary>Le regole della tabella «Regole piste». Cuore deterministico.</summary>
internal static class AirportRuleValidation
{
    /// <param name="knownIdents">Le piste che lo scalo ha davvero: una regola può nominarne una che non
    /// esiste — un refuso, o una pista tolta dopo — ed è un avviso, non un errore, perché la regola resta
    /// salvabile e va corretta da chi sa quale intendeva.</param>
    public static AirportRuleIssues Issues(IReadOnlyList<RuleEdit> rows, IEnumerable<string> knownIdents)
    {
        var errors = new List<AirportTlIssue>();
        var warnings = new List<AirportTlIssue>();
        var note = knownIdents.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var n = i + 1;

            // Una regola che non nomina nessuna pista non sceglie niente: è l'unico caso che blocca il salvataggio.
            if (r.Dep.Count == 0 && r.Arr.Count == 0)
                errors.Add(new AirportTlIssue("Ape_IssueRuleNoRw", new object[] { n }));

            foreach (var id in r.Dep.Concat(r.Arr).Where(id => !note.Contains(id)).Distinct())
                warnings.Add(new AirportTlIssue("Ape_IssueRuleUnknownRw", new object[] { n, id }));

            // Mezza finestra oraria non è una finestra: «dalle 06:00» senza un «fino a» non si sa dove finisce.
            if (r.TimeFrom is not null ^ r.TimeTo is not null)
                warnings.Add(new AirportTlIssue("Ape_IssueRuleTimeWin", new object[] { n }));
        }
        return new AirportRuleIssues(errors, warnings);
    }
}

/// <summary>
/// La conversione di una regola dalla forma d'editor a quella del dominio. ⚠️ Stava scritta DUE volte nella
/// pagina, campo per campo — una per il pannello di prova (<c>RunwayRuleEval</c>) e una per il salvataggio
/// (<c>RunwayRuleRow</c>) — e le due copie potevano divergere: la prova avrebbe detto una cosa e il salvato
/// un'altra, che su una regola di scelta pista è il difetto peggiore possibile.
/// </summary>
public static class AirportRuleMapping
{
    public static string JoinCsv(HashSet<string> set) => string.Join(",", set);
    public static int? TimeToMin(TimeOnly? t) => t is TimeOnly v ? v.Hour * 60 + v.Minute : null;

    /// <summary>Finestra stagionale ricorrente: in DB è MMDD. Vale solo se ci sono ENTRAMBI, mese e giorno.</summary>
    public static int? CombineMd(int? month, int? day) =>
        month is int m && day is int d ? m * 100 + d : null;

    public static RunwaySurface Surface(string? s) => s switch
    {
        "dry" => RunwaySurface.Dry,
        "wet" => RunwaySurface.Wet,
        _ => RunwaySurface.Any,
    };

    public static DateParity Parity(string? s) => s switch
    {
        "even" => DateParity.Even,
        "odd" => DateParity.Odd,
        _ => DateParity.Any,
    };

    /// <summary>La regola come la valuta il dominio: è ciò su cui gira il pannello di prova.</summary>
    public static RunwayRuleEval ToEval(RuleEdit r) => new(
        JoinCsv(r.Dep), JoinCsv(r.Arr), r.Name, r.Note, r.MaxTail, r.MaxCross, Surface(r.Surface),
        TimeToMin(r.TimeFrom), TimeToMin(r.TimeTo), r.DaysMask == 0 ? null : r.DaysMask, Parity(r.Parity),
        CombineMd(r.DateFromMonth, r.DateFromDay), CombineMd(r.DateToMonth, r.DateToDay));

    /// <summary>La regola come si salva. Stessi campi di <see cref="ToEval"/>, in un altro record.</summary>
    public static RunwayRuleRow ToRow(RuleEdit r) => new(
        0, JoinCsv(r.Dep), JoinCsv(r.Arr), r.Name, r.MaxTail, r.MaxCross, Surface(r.Surface), r.Note,
        TimeToMin(r.TimeFrom), TimeToMin(r.TimeTo), r.DaysMask == 0 ? null : r.DaysMask, Parity(r.Parity),
        CombineMd(r.DateFromMonth, r.DateFromDay), CombineMd(r.DateToMonth, r.DateToDay));

    /// <summary>
    /// La regola salvata, riportata in forma d'editor: è il rovescio di <see cref="ToRow"/>.
    /// <para>⚠️ Stava scritta dentro l'editor d'aeroporto. Dall'11 settembre 2026 la legge anche l'editor del
    /// vSOP militare (le regole piste di un campo senza vIPI civile si scrivono lì), e due copie della stessa
    /// lettura sono il modo in cui un campo si perde in una delle due — un caricamento che dimentica la
    /// parità riscrive la regola senza, al primo salvataggio.</para>
    /// </summary>
    public static RuleEdit FromRow(RunwayRuleRow r) => new()
    {
        Name = r.Name,
        Dep = SplitCsv(r.DepRunways), Arr = SplitCsv(r.ArrRunways), Note = r.Note,
        MaxTail = r.MaxTailwindKt, MaxCross = r.MaxCrosswindKt,
        Surface = r.Surface switch { RunwaySurface.Dry => "dry", RunwaySurface.Wet => "wet", _ => "any" },
        TimeFrom = MinToTime(r.TimeFromLocalMin), TimeTo = MinToTime(r.TimeToLocalMin),
        DaysMask = r.DaysOfWeekMask ?? 0,
        Parity = r.DateParity switch { DateParity.Even => "even", DateParity.Odd => "odd", _ => "" },
        DateFromDay = MdDay(r.DateFromMonthDay), DateFromMonth = MdMonth(r.DateFromMonthDay),
        DateToDay = MdDay(r.DateToMonthDay), DateToMonth = MdMonth(r.DateToMonthDay),
    };

    private static HashSet<string> SplitCsv(string? csv) => (csv ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static TimeOnly? MinToTime(int? min) => min is int m ? new TimeOnly(m / 60, m % 60) : null;

    // Finestra stagionale ricorrente: in DB è MMDD (mese*100+giorno), nell'editor giorno e mese separati.
    private static int? MdDay(int? mmdd) => mmdd is int md and >= 101 ? md % 100 : null;
    private static int? MdMonth(int? mmdd) => mmdd is int md and >= 101 ? md / 100 : null;
}

/// <summary>Che cosa si sta guardando nell'elenco delle SID importate: il testo cercato, la pista scelta fra
/// le chip, e se si vogliono solo quelle da rivedere.</summary>
public sealed record SidFiltro(string? Cerca = null, string? Pista = null, bool SoloDaRivedere = false);

/// <summary>
/// Filtri e regole delle SID. Cuore deterministico: è ciò che decide quali procedure un editore VEDE, e una
/// riga che sparisce da un filtro sbagliato è una riga che nessuno corregge.
/// </summary>
public static class AirportSidRules
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Le SID importate che passano il filtro.
    /// </summary>
    /// <param name="ignoraPista">Vero quando si stanno CONTANDO le piste da offrire nelle chip: se si
    /// applicasse anche il filtro pista, l'elenco delle chip conterrebbe solo quella già scelta e non si
    /// potrebbe più cambiarla. È il motivo per cui questo parametro esiste.</param>
    public static IEnumerable<ImportedSidEdit> Importate(
        IEnumerable<ImportedSidEdit> tutte, SidFiltro filtro, bool ignoraPista = false)
    {
        var q = tutte;
        if (filtro.SoloDaRivedere) q = q.Where(e => e.NeedsReview);

        var s = (filtro.Cerca ?? "").Trim();
        if (s.Length > 0)
            q = q.Where(e => e.Fix.Contains(s, OIC) || e.Name.Contains(s, OIC) || (e.Runway?.Contains(s, OIC) ?? false));

        if (!ignoraPista && filtro.Pista is { Length: > 0 } rw)
            q = q.Where(e => string.Equals(e.Runway, rw, OIC));

        return q;
    }

    /// <summary>Le piste presenti fra le SID importate che passano gli ALTRI filtri, col loro conteggio.</summary>
    public static IReadOnlyList<(string Ident, int Count)> PisteImportate(
        IEnumerable<ImportedSidEdit> tutte, SidFiltro filtro) =>
        Importate(tutte, filtro, ignoraPista: true)
            .Where(e => !string.IsNullOrWhiteSpace(e.Runway))
            .GroupBy(e => e.Runway!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count()))
            .ToList();

    /// <summary>Le SID scritte a mano che corrispondono al testo cercato.</summary>
    public static IEnumerable<SidEdit> Manuali(IEnumerable<SidEdit> tutte, string? cerca)
    {
        var q = (cerca ?? "").Trim();
        if (q.Length == 0) return tutte;
        return tutte.Where(s =>
            (s.Fix?.Contains(q, OIC) ?? false)
            || (s.Name?.Contains(q, OIC) ?? false)
            || (s.Runway?.Contains(q, OIC) ?? false));
    }

    /// <summary>Vero se il token compare nella lista separata da virgole (le transizioni, le categorie).</summary>
    public static bool HasTok(string? csv, string tok) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(tok, StringComparer.OrdinalIgnoreCase);

    /// <summary>Problemi delle SID scritte a mano: FIX o nome mancante, pista che non c'è, righe doppie.</summary>
    public static IReadOnlyList<AirportTlIssue> Issues(IReadOnlyList<SidEdit> rows, IEnumerable<string> knownIdents)
    {
        var w = new List<AirportTlIssue>();
        var note = knownIdents.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var viste = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var s = rows[i];
            var n = i + 1;
            var fix = (s.Fix ?? "").Trim();
            var nome = (s.Name ?? "").Trim();
            var rw = (s.Runway ?? "").Trim();

            if (fix.Length == 0 || nome.Length == 0)
                w.Add(new AirportTlIssue("Ape_IssueSidMissing", new object[] { n }));

            if (rw.Length > 0 && !note.Contains(rw))
                w.Add(new AirportTlIssue("Ape_IssueSidUnknownRw", new object[] { n, rw }));

            // ⚠️ La chiave del duplicato è FIX + nome + PISTA: la stessa procedura su due piste diverse è
            // legittima, ed è anzi il caso normale.
            if (fix.Length > 0 && nome.Length > 0 && !viste.Add($"{fix}|{nome}|{rw}"))
                w.Add(new AirportTlIssue("Ape_IssueSidDup", new object[] { n, fix, nome, rw }));
        }
        return w;
    }
}

/// <summary>
/// Il cancello del salvataggio a ogni gesto (carta 2026-09-04-aeroporto-porta-sola): <b>questa collezione si
/// può scrivere adesso?</b>
///
/// <para>⚠️ Serve perché «salva a ogni gesto» incontra un caso che gli altri editor non hanno: le tabelle
/// dell'aeroporto si riempiono una casella per volta, e i service alzano <c>ValidationException</c> su una
/// riga incompleta (Transition Level obbligatorio, ident pista obbligatorio, almeno una pista DEP o ARR per
/// la regola, nome e FIX per la SID). Salvare a ogni tasto significherebbe un avviso rosso in faccia a chi
/// ha appena premuto «+ riga» e non ha ancora finito di scriverla.</para>
///
/// <para>Con questo cancello il gesto su una riga incompleta <b>non scrive</b>, e l'editor lo dice in una
/// riga di testo; appena la riga è completa il gesto successivo salva tutto. È lo stesso criterio già scritto
/// in <see cref="AirportRunwayValidation"/>: <i>una riga senza identificativo non è un errore, è una riga che
/// non si è ancora finito di scrivere.</i></para>
///
/// <para>⚠️ Non è la guardia: la guardia resta nei service, che rifiutano comunque. Qui si decide solo
/// <b>quando ha senso provarci</b>, e per questo le due regole devono restare allineate — se un service
/// cambia una condizione obbligatoria, cambia anche qui.</para>
///
/// <para>Cuore deterministico: righe che entrano, sì/no che esce. Nessuna UI, nessun I/O.</para>
/// </summary>
public static class AirportSaveGate
{
    /// <summary>Ogni livello di transizione ha il suo FL.</summary>
    public static bool Tls(IReadOnlyList<TlEdit> rows) => rows.All(r => !string.IsNullOrWhiteSpace(r.Level));

    /// <summary>Ogni pista ha un identificativo.</summary>
    public static bool Runways(IReadOnlyList<RwEdit> rows) => rows.All(r => !string.IsNullOrWhiteSpace(r.Ident));

    /// <summary>Ogni regola dice almeno una pista, in partenza o in arrivo.</summary>
    public static bool Rules(IReadOnlyList<RuleEdit> rows) => rows.All(r => r.Dep.Count > 0 || r.Arr.Count > 0);

    /// <summary>Ogni SID manuale ha nome e punto.</summary>
    public static bool Sids(IReadOnlyList<SidEdit> rows) =>
        rows.All(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Fix));
}
