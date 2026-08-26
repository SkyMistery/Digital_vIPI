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
public sealed record AirportRulesView(IReadOnlyList<AirportRuleRowView> Rows)
{
    public static AirportRulesView Empty { get; } = new(Array.Empty<AirportRuleRowView>());
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
public sealed record AirportRunwayRowView(string Ident, int? LengthM, string Tora, string Lda,
    string AppProcedures, string Patterns, string Circling);

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
    public static AirportRulesView Rules(AirportData? data)
    {
        if (data is null || data.Rules.Count == 0) return AirportRulesView.Empty;
        return new AirportRulesView(data.Rules
            .Select((r, i) => new AirportRuleRowView(i + 1, RuleCondition(r), Dash(r.DepRunways), Dash(r.ArrRunways), Dash(r.Note)))
            .ToList());
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
                Dash(r.AppProcedures), Dash(r.Patterns), Dash(r.Circling)))
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
