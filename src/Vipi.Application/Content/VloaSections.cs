using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Blocco di contenuto canonico di una sezione vLOA (spec puro, materializzato lato Infrastructure).</summary>
public sealed record VloaBlockSpec(BlockFormat Format, string? Body = null, string? BodyJson = null, CalloutKind? CalloutKind = null);

/// <summary>Sezione canonica (obbligatoria) di una vLOA, con blocchi e figli (per i Coordinamenti).</summary>
public sealed record VloaSectionSpec(string SectionKey, string Title,
    IReadOnlyList<VloaBlockSpec> Blocks, IReadOnlyList<VloaSectionSpec> Children);

/// <summary>
/// <b>Contenuto</b> iniziale della vLOA (mockup 3d «vLOA Estere»), parametrico su Home/Foreign. La <b>struttura</b>
/// — quali sezioni, con che chiave, che titolo e in che ordine — la dice il <see cref="SectionCatalog"/>, profilo
/// <see cref="SectionProfile.Vloa"/>, come per la vIPI ACC e l'APP (doc 13 §3c).
/// <para>
/// Fino al doc 13 questo file era un <b>registro parallelo</b> espresso nell'enum legacy <c>BlockSection</c>, e il
/// catalogo per la vLOA non lo consultava nessuno: le due descrizioni erano divergenti su tre punti (una sezione in
/// più — «Purpose» —, un ordine diverso e il titolo delle aree regolamentate), e la sola vLOA identificava le sezioni
/// obbligatorie per <b>titolo</b> invece che per chiave.
/// </para>
/// Il seeding delle entità EF resta in Infrastructure (<c>VloaStructureSeeder</c>).
/// </summary>
public static class VloaSections
{
    private const string CoordinationKey = SectionKeys.Coordination;

    /// <summary>Struttura canonica parametrizzata sui codici ACC della coppia (contenuto EN placeholder).
    /// ⚠️ Niente ciclo AIRAC fra i parametri: il contenuto iniziale non ne contiene più (doc 14 §3b), e tenerlo
    /// avrebbe lasciato in giro la porta da cui rientrare.</summary>
    public static IReadOnlyList<VloaSectionSpec> Canonical(string homeCode, string foreignCode, string? foreignName)
    {
        var home = (homeCode ?? "").Trim().ToUpperInvariant();
        var foreign = (foreignCode ?? "").Trim().ToUpperInvariant();
        var fName = string.IsNullOrWhiteSpace(foreignName) ? foreign : foreignName.Trim();

        return SectionCatalog.For(SectionProfile.Vloa)
            .OrderBy(d => d.Order)
            .Select(d => new VloaSectionSpec(d.Key, d.Title, BlocksFor(d.Key, home, foreign, fName),
                ChildrenFor(d.Key, home, foreign)))
            .ToList();
    }

    // Contenuto iniziale per chiave di catalogo. Le sezioni DERIVATE (aor/frequencies) portano solo l'introduzione:
    // la tabella la genera il viewer dai dati. «coordination» non ha corpo proprio — le due direzioni sono le sue
    // sotto-sezioni (doc 11 §3f).
    private static IReadOnlyList<VloaBlockSpec> BlocksFor(string key, string home, string foreign, string fName) => key switch
    {
        "purpose" => new[] { Prose(Scopo(home, foreign, fName)) },
        "aor" => new[] { Prose(Aree(home, foreign)), Prose(Settori(home, foreign, fName)) },
        "frequencies" => new[] { Prose(Frequenze(home, foreign, fName)) },
        "operationaltechnique" => new[]
        {
            Prose(Tecnica),
            Callout(CalloutKind.Warning, "Reduced coordination", CoordinamentoRidotto),
        },
        "regulated" => new[] { Prose(AreeMilitari) },
        // ⚠️ Qui NON si scrive il ciclo AIRAC (doc 14 §3b). C'era, come riga «Effective from — AIRAC ####»,
        // e portava il ciclo del GIORNO DELLA CREAZIONE: un numero che non si aggiornava mai, mentre la scheda
        // sopra mostra quello della release che si sta guardando. Le quattro vLOA dell'archivio dicevano tutte
        // «AIRAC 2607» e una di loro era pubblicata al 2608 — due numeri diversi nella stessa pagina, ed era
        // esattamente la tabella scritta a mano che il timbro di validità era nato per eliminare.
        // Restano le due cose che nessuno può derivare: il ciclo di revisione concordato e il firmatario.
        "validity" => new[]
        {
            Table(new[] { "Item", "Value" },
                Cells("Review cycle", "Bilateral, at least annually"),
                Cells("Italian signatory", $"{home} CH / AOD")),
        },
        _ => Array.Empty<VloaBlockSpec>(),
    };

    // ---- Le frasi di partenza, e il loro italiano -----------------------------------------------------
    //
    // ⚠️ Stanno QUI, accanto all'inglese, e non in un file di traduzioni: sono la stessa frase in due lingue,
    // e due file che dicono la stessa cosa un giorno dicono cose diverse. `BlocksFor` scrive l'inglese
    // leggendolo da qui, e `FrasiDaSeminare` legge tutt'e due: il seme non puo' mancare il bersaglio perche'
    // qualcuno ha corretto una virgola da una parte sola.
    //
    // ⚠️ Perche' esistono. Questo e' testo NOSTRO, e mandarlo a un traduttore a pagamento e' un errore in due
    // modi: si paga per una risposta che sappiamo gia', e la si compra sbagliata — «Piste» → «Slopes», visto
    // dal vivo il 28 agosto 2026, e' lo stesso motivo per cui esiste `TitoliUfficiali`. In piu' due di queste
    // frasi hanno DUE SEGNAPOSTI ATTACCATI (`LIBB/LGGG`), che e' il costrutto che un motore tende a fondere:
    // il 30 agosto 2026 una di loro tornava rotta a ogni giro, 155 caratteri buttati ogni quarto d'ora.

    private static string Scopo(string home, string foreign, string fName) =>
        $"This Letter of Agreement establishes the coordination procedures, transfer of control and transfer of communications between **{home}** and **{foreign} ({fName})** for traffic crossing the common boundary.";

    private static string ScopoIt(string home, string foreign, string fName) =>
        $"La presente Lettera d'Accordo stabilisce le procedure di coordinamento, il trasferimento del controllo e il trasferimento delle comunicazioni fra **{home}** e **{foreign} ({fName})** per il traffico che attraversa il confine comune.";

    private static string Aree(string home, string foreign) =>
        $"Both areas of responsibility are imported from the IVAO database; the common boundary is the {home}/{foreign} ACC limit.";

    private static string AreeIt(string home, string foreign) =>
        $"Le due aree di competenza sono importate dal database IVAO; il confine comune è il limite fra gli ACC {home}/{foreign}.";

    private static string Settori(string home, string foreign, string fName) =>
        $"**{home}:** sectors bordering {foreign}. **{foreign} ({fName}):** sectors bordering {home}.";

    private static string SettoriIt(string home, string foreign, string fName) =>
        $"**{home}:** settori confinanti con {foreign}. **{foreign} ({fName}):** settori confinanti con {home}.";

    private static string Frequenze(string home, string foreign, string fName) =>
        $"Working frequencies of **{home}** and **{foreign} ({fName})** for the sectors along the common boundary (derived from the IVAO database).";

    private static string FrequenzeIt(string home, string foreign, string fName) =>
        $"Frequenze operative di **{home}** e **{foreign} ({fName})** per i settori lungo il confine comune (ricavate dal database IVAO).";

    private const string Tecnica =
        "Transfer of control takes place at the common boundary unless otherwise agreed. Transfer of communications is initiated **not later than 5 minutes** before the Coordination Point.";

    private const string TecnicaIt =
        "Il trasferimento del controllo avviene al confine comune, salvo diverso accordo. Il trasferimento delle comunicazioni si avvia **non oltre 5 minuti** prima del Punto di Coordinamento.";

    private const string CoordinamentoRidotto =
        "In case of radar/communication degradation, revert to estimates and verbal handoff at the boundary.";

    private const string CoordinamentoRidottoIt =
        "In caso di degrado radar o delle comunicazioni, si torna alle stime e al passaggio verbale al confine.";

    private const string AreeMilitari =
        "Activation and crossing of cross-border military areas adjacent to the common boundary are coordinated between the two units.";

    private const string AreeMilitariIt =
        "L'attivazione e l'attraversamento delle aree militari transfrontaliere adiacenti al confine comune sono coordinati fra i due enti.";

    /// <summary>
    /// Le frasi di partenza di una vLOA, <b>inglese e italiano</b>, per la coppia di ACC data. Le legge il
    /// seme della memoria di traduzione: sono parola nostra, e non si comprano da un motore.
    /// <para>⚠️ Solo la PROSA: le tabelle e i titoli non passano di qui.</para>
    /// </summary>
    public static IReadOnlyList<(string En, string It)> FrasiDaSeminare(
        string homeCode, string foreignCode, string? foreignName)
    {
        var home = (homeCode ?? "").Trim().ToUpperInvariant();
        var foreign = (foreignCode ?? "").Trim().ToUpperInvariant();
        var fName = string.IsNullOrWhiteSpace(foreignName) ? foreign : foreignName.Trim();

        return new[]
        {
            (Scopo(home, foreign, fName), ScopoIt(home, foreign, fName)),
            (Aree(home, foreign), AreeIt(home, foreign)),
            (Settori(home, foreign, fName), SettoriIt(home, foreign, fName)),
            (Frequenze(home, foreign, fName), FrequenzeIt(home, foreign, fName)),
            (Tecnica, TecnicaIt),
            (CoordinamentoRidotto, CoordinamentoRidottoIt),
            (AreeMilitari, AreeMilitariIt),
        };
    }

    // Le due direzioni dei coordinamenti. Nessun blocco: il corpo lo produce l'editor (tabella dei trasferimenti)
    // e il viewer le rende dal padre — un paragrafo scritto qui non lo vedrebbe nessuno.
    private static IReadOnlyList<VloaSectionSpec> ChildrenFor(string key, string home, string foreign) =>
        key == CoordinationKey
            ? new[]
            {
                Sec(SectionKeys.CoordinationOut, $"{home} → {foreign}"),
                Sec(SectionKeys.CoordinationIn, $"{foreign} → {home}"),
            }
            : Array.Empty<VloaSectionSpec>();

    // ---- costruttori spec ----
    private static VloaSectionSpec Sec(string key, string title) =>
        new(key, title, Array.Empty<VloaBlockSpec>(), Array.Empty<VloaSectionSpec>());

    private static VloaBlockSpec Prose(string markdown) => new(BlockFormat.Prose, Body: markdown);

    private static VloaBlockSpec Callout(CalloutKind kind, string title, string markdown) =>
        new(BlockFormat.Callout, Body: markdown, BodyJson: JsonSerializer.Serialize(new { title }), CalloutKind: kind);

    private static VloaBlockSpec Table(string[] columns, params object[] rows) =>
        new(BlockFormat.Table, BodyJson: JsonSerializer.Serialize(new { columns, unified = false, rows }));

    private static object Cells(params string[] cells) => new { cells };
}
