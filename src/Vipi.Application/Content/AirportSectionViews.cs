using Vipi.Domain;

namespace Vipi.Application.Content;

// Viste delle sezioni DERIVATE della vIPI d'aeroporto (carta 2026-08-26 §2).
//
// Perché esistono, visto che la pagina ha già `AirportData` sotto mano: perché una sezione derivata si può
// CONGELARE alla release, e ciò che si congela dev'essere esattamente ciò che si mostra. Sono quindi la forma
// serializzabile del corpo — lo stesso ruolo che `AccAorView`/`AppCoordination` hanno per l'APP.
//
// Fino a questa carta il corpo era una tabella Markdown COTTA nei blocchi del documento, e per questo l'ordine,
// il «nascondi» e le sotto-sezioni non sopravvivevano a un rebuild: le sezioni venivano distrutte e riscritte.

/// <summary>Una regola di scelta pista, già in forma leggibile: la condizione è testo, non soglie da comporre.</summary>
public sealed record AirportRuleRowView(int Position, string Condition, string Dep, string Arr, string Note);

/// <summary>Sezione «Regole piste»: si applica la <b>prima</b> regola le cui condizioni sono soddisfatte.</summary>
/// <param name="Regole">
/// Le stesse regole in forma <b>calcolabile</b>, nello stesso ordine di <paramref name="Rows"/>: servono a
/// dire quale sta vincendo <i>adesso</i> e quale pista marcare.
///
/// <para>⚠️ <b>Perché stanno nella vista e non si rileggono dall'anagrafica.</b> La tabella segue la sezione:
/// <c>Frozen</c> ⇒ è la fotografia della release, <c>Live</c> ⇒ è quella di adesso. Il verdetto, invece, si
/// calcolava <b>sempre</b> sulle regole vive: con la sezione congelata bastava cambiare una regola dopo aver
/// pubblicato perché la pastiglia «adesso» finisse su un'altra riga e le Piste marcassero una pista che la
/// tabella pubblicata non spiega. Tenendole insieme alle righe, chi valuta guarda per forza <b>le stesse</b>
/// regole che il lettore ha davanti. Il vento, quello sì, resta sempre quello di adesso.</para>
///
/// <para>⚠️ <c>null</c> = <b>non si sa</b>, ed è diverso da «nessuna regola»: sono le fotografie scattate
/// prima del 12 settembre 2026, che questo campo non l'hanno. Chi legge ricade sulle regole vive — cioè sul
/// comportamento di prima — e non mostra la pastiglia, che non avrebbe niente a cui riferirsi. Una lista
/// <b>vuota</b> invece è un fatto: «quel documento dice che non ci sono regole».</para>
/// </param>
public sealed record AirportRulesView(
    IReadOnlyList<AirportRuleRowView> Rows, IReadOnlyList<RunwayRuleRow>? Regole = null)
{
    public static AirportRulesView Empty { get; } = new(Array.Empty<AirportRuleRowView>());

    /// <summary>Nessuna regola, e lo si sa: il documento (o l'anagrafica) non ne ha.</summary>
    public static AirportRulesView Nessuna { get; } =
        new(Array.Empty<AirportRuleRowView>(), Array.Empty<RunwayRuleRow>());
}

/// <summary>Riga della tabella dei livelli di transizione: fascia QNH → livello.</summary>
public sealed record AirportTlRowView(string QnhRange, string Level);

/// <summary>Sezione «Quote di transizione»: la TA e la tabella per fascia QNH.</summary>
public sealed record AirportTransitionView(int? TransitionAltitudeFt, IReadOnlyList<AirportTlRowView> Rows)
{
    public static AirportTransitionView Empty { get; } = new(null, Array.Empty<AirportTlRowView>());
}

/// <summary>Riga della tabella frequenze. <paramref name="IsPrimary"/> = la principale per quel tipo di posizione (★).</summary>
public sealed record AirportFreqRowView(string Name, string Callsign, string Frequency, bool IsPrimary);

/// <summary>Sezione «Frequenze»: catalogo dei settori dello scalo + i link a frequenze di altri enti.</summary>
public sealed record AirportFreqView(IReadOnlyList<AirportFreqRowView> Rows)
{
    public static AirportFreqView Empty { get; } = new(Array.Empty<AirportFreqRowView>());
}

/// <summary>Riga della tabella piste: i campi di sorgente IVAO più le colonne editoriali.</summary>
/// <param name="Threshold">
/// Le coordinate della soglia già scritte in sessagesimale, o stringa vuota se la sorgente non le ha ancora
/// portate. ⚠️ Si formattano <b>qui</b> e non nella pagina perché questa riga finisce negli SNAPSHOT di
/// release: una release deve fotografare quel che si legge, non due numeri da ri-formattare al view — e la
/// formattazione può cambiare.
/// </param>
public sealed record AirportRunwayRowView(string Ident, int? LengthM, string Tora, string Lda,
    string AppProcedures, string Patterns, string Circling,
    string Threshold = "", int? ThresholdElevationFt = null);

/// <summary>
/// Una colonna a elenco della tabella piste — le procedure d'avvicinamento — letta come elenco: le voci si
/// scrivono <b>«ILS, VOR»</b>, una virgola e uno spazio fra l'una e l'altra, qualunque sia la forma in cui sono
/// in archivio (chiesto dal committente l'11 settembre 2026).
///
/// <para>⚠️ Esiste perché il testo libero di prima delle chip ha lasciato forme diverse — «VOR,RNP», una
/// tabulazione in testa, il punto e virgola — e lo storage è rimasto quella stringa. Le chip riscrivono la forma
/// giusta al primo clic; questa la dà a chi <b>legge</b> senza aspettare che qualcuno clicchi.</para>
///
/// <para>⚠️ Si applica al <b>disegno</b>, non alla proiezione: le release congelano la stringa di allora, e
/// così anche i documenti già pubblicati escono nella forma giusta senza ripubblicarli. Cambiarla nella
/// proiezione avrebbe fatto sembrare «da ripubblicare» ogni aeroporto con la forma vecchia, per una virgola.</para>
///
/// <para>È anche la regola con cui le chip (<c>RunwayChoices</c>) dividono il campo: una sola definizione di
/// «dove finisce una voce», o lettura e scrittura non si troverebbero d'accordo.</para>
/// </summary>
public static class AirportRunwayLists
{
    /// <summary>Le voci del campo: divise sulle virgole e sui punti e virgola, spazi ripetuti ridotti a uno.
    /// ⚠️ Non si divide sugli spazi: «L JET» è UNA voce.</summary>
    public static IReadOnlyList<string> Tokens(string? value) =>
        (value ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => string.Join(' ', t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(t => t.Length > 0)
            .ToList();

    /// <summary>Il campo come si legge: «ILS, VOR». Vuoto = «—», come ogni cella editoriale vuota.</summary>
    public static string Format(string? value)
    {
        var voci = Tokens(value);
        return voci.Count == 0 ? "—" : string.Join(", ", voci);
    }
}

/// <summary>Sezione «Piste».</summary>
public sealed record AirportRunwaysView(IReadOnlyList<AirportRunwayRowView> Rows)
{
    public static AirportRunwaysView Empty { get; } = new(Array.Empty<AirportRunwayRowView>());
}

/// <summary>
/// Le sezioni derivate della vIPI d'aeroporto risolte per UNA vista. Il meteo non c'è: è l'unica sezione
/// <see cref="SectionCatalog.IsAlwaysLive"/>, non si congela e la pagina la chiede al provider meteo — un METAR
/// dentro uno snapshot di release sarebbe meteo scaduto spacciato per attuale.
/// </summary>
public sealed record AirportDerived(
    AirportRulesView Rules, AirportTransitionView Transition, AirportFreqView Frequencies,
    AirportRunwaysView Runways, AirportSidView Sids)
{
    public static AirportDerived Empty { get; } = new(
        AirportRulesView.Empty, AirportTransitionView.Empty, AirportFreqView.Empty,
        AirportRunwaysView.Empty, AirportSidView.Empty);
}

/// <summary>
/// Proiezione PURA (niente I/O) dal profilo strutturato dell'aeroporto alle viste delle sue sezioni derivate.
/// Sta qui e non nel repository perché è la stessa risposta per il viewer, per l'editor e per la cattura di
/// release: prima viveva dentro la cottura, e quindi esisteva una sola volta l'anno — al rebuild.
/// </summary>
public static class AirportSectionProjection
{
    /// <summary>
    /// Le regole, in forma leggibile e in forma calcolabile: le due viaggiano insieme apposta (vedi
    /// <see cref="AirportRulesView.Regole"/>).
    /// <para>⚠️ Un'anagrafica che non c'è dà <see cref="AirportRulesView.Empty"/> («non si sa»); una senza
    /// regole dà <see cref="AirportRulesView.Nessuna"/> («non ce ne sono»). La distinzione conta solo per chi
    /// legge una release vecchia, e lì vale la differenza fra ricadere sulle regole vive e non valutare nulla.</para>
    /// </summary>
    public static AirportRulesView Rules(AirportData? data)
    {
        if (data is null) return AirportRulesView.Empty;
        if (data.Rules.Count == 0) return AirportRulesView.Nessuna;
        return new AirportRulesView(
            data.Rules
                .Select((r, i) => new AirportRuleRowView(i + 1, RuleCondition(r), Dash(r.DepRunways), Dash(r.ArrRunways), Dash(r.Note)))
                .ToList(),
            data.Rules);
    }

    public static AirportTransitionView Transition(AirportData? data)
    {
        if (data is null) return AirportTransitionView.Empty;
        return new AirportTransitionView(
            data.TransitionAltitudeFt,
            data.TransitionLevels.Select(t => new AirportTlRowView(QnhRange(t.QnhFrom, t.QnhTo), t.Level)).ToList());
    }

    /// <summary>
    /// Frequenze: il catalogo dei settori dello scalo (ATIS·DEL·GND·TWR·APP/DEP, ★ = principale per tipo) più i
    /// link a enti esterni. ⚠️ Le righe nascoste e quelle senza frequenza restano fuori: sono nel catalogo per
    /// l'amministrazione dei settori, non per il documento.
    /// </summary>
    public static AirportFreqView Frequencies(
        IEnumerable<AirportSectorRow>? catalog, IReadOnlyList<FrequencyLinkRow>? links)
    {
        var rows = (catalog ?? Array.Empty<AirportSectorRow>())
            .Where(s => !s.IsHidden && !string.IsNullOrWhiteSpace(s.Frequency))
            .OrderBy(s => FrequencyPositions.OrderOf(s.Position))
            .ThenByDescending(s => s.IsPrimary)
            .ThenBy(s => s.ComposePosition, StringComparer.Ordinal)
            // Il nome è quello che IVAO dà alla postazione («Pisa Approach»); il nome-posizione è il ripiego per
            // le righe che non ce l'hanno. La cottura usava solo il ripiego, e infatti il documento pubblicato
            // diceva «Approach» dove l'editor e la pagina dicevano il nome vero.
            .Select(s => new AirportFreqRowView(
                string.IsNullOrWhiteSpace(s.AtcCallsign) ? FrequencyPositions.NameOf(s.Position) : s.AtcCallsign!,
                s.ComposePosition, s.Frequency!, s.IsPrimary))
            .ToList();

        foreach (var l in links ?? Array.Empty<FrequencyLinkRow>())
            rows.Add(new AirportFreqRowView(l.Label, l.Callsign, l.FrequencyMhz, false));

        return rows.Count == 0 ? AirportFreqView.Empty : new AirportFreqView(rows);
    }

    public static AirportRunwaysView Runways(AirportData? data)
    {
        if (data is null || data.Runways.Count == 0) return AirportRunwaysView.Empty;
        return new AirportRunwaysView(data.Runways
            .Select(r => new AirportRunwayRowView(
                r.Ident, r.LengthM,
                // TORA e LDA sono testo editoriale; se non compilati vale la lunghezza d'anagrafica.
                Fallback(r.ToraM, r.LengthM), Fallback(r.LdaM, r.LengthM),
                Dash(r.AppProcedures), Dash(r.Patterns), Dash(r.Circling),
                NavaidText.Coordinate(r.ThresholdLat, r.ThresholdLon), r.ThresholdElevationFt))
            .ToList());
    }

    // ---- formattazioni ----
    // Venivano dalla cottura in EfAirportRepository. Sono formattazioni, non persistenza: qui sono verificabili
    // senza un database, e sono le stesse per il documento pubblicato e per l'editor.

    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();

    private static string Fallback(string? text, int? lengthM) =>
        !string.IsNullOrWhiteSpace(text) ? text!.Trim() : lengthM is int m ? $"{m} m" : "—";

    /// <summary>Testo della fascia QNH, coi simboli che il lettore riconosce (≥ / ≤ / –).</summary>
    public static string QnhRange(int? from, int? to) =>
        (from, to) switch
        {
            (int f, null) => $"≥ {f}",
            (null, int t) => $"≤ {t}",
            (int f, int t) => $"{f} – {t}",
            _ => "—",
        };

    /// <summary>Condizione della regola in testo: soglie coda/traverso, superficie, nome ed eventuali finestre temporali.</summary>
    public static string RuleCondition(RunwayRuleRow r)
    {
        var parts = new List<string> { $"vento in coda ≤ {r.MaxTailwindKt} kt" };
        if (r.MaxCrosswindKt is int xw) parts.Add($"traverso ≤ {xw} kt");
        if (r.Surface == RunwaySurface.Dry) parts.Add("pista asciutta");
        else if (r.Surface == RunwaySurface.Wet) parts.Add("pista bagnata");
        if (r.TimeFromLocalMin is int tf && r.TimeToLocalMin is int tt) parts.Add($"{Hhmm(tf)}–{Hhmm(tt)} LT");
        else if (r.TimeFromLocalMin is int tf2) parts.Add($"dalle {Hhmm(tf2)} LT");
        else if (r.TimeToLocalMin is int tt2) parts.Add($"fino alle {Hhmm(tt2)} LT");
        if (DaysLabel(r.DaysOfWeekMask) is string dl) parts.Add(dl);
        if (r.DateParity == DateParity.Even) parts.Add("giorni pari");
        else if (r.DateParity == DateParity.Odd) parts.Add("giorni dispari");
        if (DateWindowLabel(r.DateFromMonthDay, r.DateToMonthDay) is string dw) parts.Add(dw);
        var cond = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(r.Name) ? cond : $"{r.Name!.Trim()}: {cond}";
    }

    private static string Hhmm(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    private static readonly string[] DayNames = { "lun", "mar", "mer", "gio", "ven", "sab", "dom" };

    /// <summary>Etichetta dei giorni; null se la maschera è vuota o copre tutti e sette — nessun vincolo da mostrare.</summary>
    private static string? DaysLabel(int? mask)
    {
        if (mask is not int m || m == 0 || m == 0b111_1111) return null;
        return string.Join("/", Enumerable.Range(0, 7).Where(b => (m & (1 << b)) != 0).Select(b => DayNames[b]));
    }

    private static readonly string[] MonthAbbr =
        { "gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic" };

    /// <summary>Finestra stagionale ricorrente, codificata <c>MMDD</c> (mese × 100 + giorno). Null = nessun vincolo.</summary>
    private static string? DateWindowLabel(int? from, int? to)
    {
        if (from is null && to is null) return null;
        if (from is int f && to is int t) return $"dal {Md(f)} al {Md(t)}";
        if (from is int f2) return $"dal {Md(f2)}";
        return $"fino al {Md(to!.Value)}";

        static string Md(int mmdd) => $"{mmdd % 100} {MonthAbbr[Math.Clamp(mmdd / 100, 1, 12) - 1]}";
    }
}
