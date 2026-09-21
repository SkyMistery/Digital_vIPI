using Vipi.Domain;

namespace Vipi.Application.Content;

// Modelli della vIPI ACC: documento a blocchi (Aerovia/CTR + gruppi APP). Riusa i record editoriali/derivati
// dell'APP (AppSeparationRow, AppFreqOrderOverride, AppFreqRow, AppCoordination, AppAorPolygon).

/// <summary>Tipo di blocco della vIPI ACC.</summary>
public enum AccBlockKind { Aerovia, AppGroup }

/// <summary>Un settore APERTO in una configurazione: callsign + Center Point/Range (input manuale staff).</summary>
public sealed class AccConfigOpen
{
    public string Callsign { get; set; } = "";
    public string? CenterPoint { get; set; }
    public string? Range { get; set; }
}

/// <summary>Configurazione operativa = insieme di settori APERTI; il sistema deriva l'accorpamento (chi copre chi).</summary>
public sealed class AccConfiguration
{
    public string Key { get; set; } = "";                          // stabile, es. "cfg:{guid}"
    public string Name { get; set; } = "";                         // es. "2 settori (NE/SW)"
    public List<AccConfigOpen> Open { get; set; } = new();         // settori aperti + CP/Range per settore

    public IEnumerable<string> OpenCallsigns => Open.Select(o => o.Callsign);
}

/// <summary>
/// Una sezione di un blocco vIPI ACC, nell'ordine del documento (doc 11 §3b). <see cref="Editorial"/> porta i
/// blocchi e le sotto-sezioni già pronti per la resa condivisa (<c>SectionNode</c>/<c>SectionBody</c>): per le
/// sezioni strutturate (aor/frequenze/…) il corpo lo produce la pagina, ma le sotto-sezioni restano qui.
/// </summary>
/// <param name="Audience">
/// A chi si rivolge la sezione (carta vSOP militari §3). ⚠️ Sta <b>anche</b> qui e non solo dentro
/// <paramref name="Editorial"/>: le sezioni di catalogo mai scritte arrivano con <c>Editorial = null</c>, e
/// senza questo campo il filtro della pagina dovrebbe indovinarne il destinatario.
/// </param>
/// ⚠️ Il default è <c>Both</c> — «per tutti» — e non è pigrizia: è lo zero dell'enum e il significato
/// giusto di «nessuno l'ha marcata». Chi costruisce una sezione senza dire il destinatario sta dicendo
/// esattamente quello.
public sealed record AccBlockSection(int SectionId, string Key, string Title, bool IsHidden,
                                     SectionView? Editorial,
                                     SectionAudience Audience = SectionAudience.Both);

/// <summary>
/// Un blocco della vIPI ACC: Aerovia (settori CTR, pool implicito) o gruppo-APP (settori APP scelti).
/// Contiene lo stato editoriale (sezioni/override) + le configurazioni che guidano l'AoR.
/// </summary>
public sealed class AccBlock
{
    public string Key { get; set; } = "";                          // "aerovia" | "grp:{guid}"
    public AccBlockKind Kind { get; set; }
    public string Title { get; set; } = "";

    /// <summary>Settori membri (callsign). Aerovia: vuoto = tutti i CTR dell'ACC. Gruppo-APP: gli APP scelti.</summary>
    public List<string> MemberCallsigns { get; set; } = new();

    /// <summary>Sezioni del blocco NELL'ORDINE del documento (doc 11 §3b): il viewer itera questa lista, non un
    /// elenco di chiavi. Le sezioni-catalogo eventualmente assenti dai documenti vecchi sono accodate al loro posto
    /// con <c>SectionId = 0</c>.</summary>
    public List<AccBlockSection> Sections { get; set; } = new();

    public List<AccConfiguration> Configurations { get; set; } = new();

    /// <summary>Callsign di settori DB (anche esteri) aggiunti a mano come shape AoR extra: appesi come anelli
    /// toggleabili dopo i settori principali e selezionabili nelle configurazioni. Storage: sezione figlia <c>aor</c>.</summary>
    public List<string> ExtraAorCallsigns { get; set; } = new();

    /// <summary>Override di colore per settore (callsign → hex), sia primari sia extra. Assente = default per tipo-ente
    /// (<see cref="Vipi.Application.Aor.AorColorScheme"/>). Storage: sezione figlia <c>aor</c>.</summary>
    public Dictionary<string, string> AorColorOverrides { get; set; } = new();

    /// <summary>Classe e nota scritte a mano per i volumi dell'AIP agganciati ai settori del blocco (chiave naturale →
    /// correzione). Storage: sezione figlia <c>aor</c>. Carta 2026-09-17-tabella-spazi-aerei-nell-aor.md.</summary>
    public Dictionary<string, AorAirspaceEdit> AorAirspaceEdits { get; set; } = new();

    // editoriale
    public List<AppSeparationRow> Separations { get; set; } = new();
    public RegulatedSelection Regulated { get; set; } = new();                    // #8: aree speciali (proprio ACC auto/manuale + extra altri-ACC)

    // frequenze
    public List<AppFreqOrderOverride> FreqOrder { get; set; } = new();
    public List<string> FreqLinkCallsigns { get; set; } = new();   // link extra per callsign (riferimento vivo)
}

/// <summary>Dati completi della vIPI ACC per editor e viewer: identità + blocchi.</summary>
public sealed class AccVipiData
{
    public required string AccCode { get; init; }
    public required string AccName { get; init; }
    public required List<AccBlock> Blocks { get; init; }
}

/// <summary>Settore selezionabile (per picker membri gruppo / settori aperti config): callsign + nome.</summary>
public sealed record AccSectorPick(string Callsign, string Name);

/// <summary>
/// Una carta delle minime di vettoramento, cioè il contenuto di UN file <c>.mva</c> del sectorfile. Il nome del
/// file è l'unica attribuzione che il formato dichiari, quindi è anche l'unità di visualizzazione: come in Aurora,
/// dove accendere le MRVA di un ente mostra tutto il suo file.
/// </summary>
/// <param name="Owner">L'ente a cui il file appartiene: codice ACC (<c>LIMM</c>) o ICAO (<c>LIRN</c>). Serve come
/// chiave del contenitore, NON come intestazione: a schermo la carta non porta un nome d'aeroporto, perché il
/// file copre un'area che spesso va oltre lo scalo che gli dà il nome e la didascalia risultava fuorviante.</param>
/// <param name="Chart">Tracciati ed etichette verbatim dal sectorfile.</param>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record MinimaChart(string Owner, Abstractions.MvaChart Chart);

/// <summary>
/// Sezione «Minime di vettoramento» derivata: <b>una carta per file</b>. Un blocco Aerovia ne ha una (l'enroute
/// del suo ACC); un gruppo-APP o un APP standalone ne ha una per aeroporto membro che abbia il file — e nessuna
/// per quelli che non ce l'hanno, che nel sectorfile italiano sono la maggioranza.
/// </summary>
public sealed record MinimaView(IReadOnlyList<MinimaChart> Charts)
{
    public static readonly MinimaView Empty = new(Array.Empty<MinimaChart>());

    /// <summary>Vero se non c'è nessuna carta: la sezione lo dice, invece di mostrare una mappa vuota.</summary>
    public bool IsEmpty => Charts.Count == 0;
}

/// <summary>Settore selezionabile come shape AoR extra (picker globale): callsign + nome IVAO + ACC di appartenenza
/// (per cercare l'ente). Sorgente = tutti i settori DB con poligono.</summary>
public sealed record SectorShapePick(string Callsign, string Name, string? AccCode);

/// <summary>Limiti di quota GREZZI di un settore (ft o FL, come in DB): per l'estrusione 3D, normalizzati poi da
/// <see cref="Vipi.Application.Aor.AorFlBand"/>. Entrambi null = nessun limite noto.</summary>
public sealed record SectorFlLimits(int? Lower, int? Upper);

/// <summary>Stato editoriale della sezione <c>aor</c>: callsign dei settori DB aggiunti a mano come shape extra +
/// override di colore per settore (callsign → hex, sia primari sia extra). Colore assente = default per tipo-ente.</summary>
public sealed class AorExtraShapes
{
    public List<string> Callsigns { get; set; } = new();
    public Dictionary<string, string> Colors { get; set; } = new();

    /// <summary>
    /// Classe e nota dei volumi dell'AIP agganciati, per <b>chiave naturale</b> (<c>FAMIGLIA|NOME|BASE|TETTO</c>):
    /// un KMZ ricaricato rifà le righe ma non la chiave, e la nota sopravvive.
    ///
    /// <para>⚠️ Non chiamarlo <c>Airspaces</c>: negli snapshot ACC questo stesso JSON contiene l'<see cref="AccAorView"/>
    /// (che ha un <c>Airspaces</c> a lista), e <c>AccDocumentAssembler</c> ci legge sopra questa classe — due campi
    /// omonimi di forma diversa farebbero fallire la lettura.</para>
    /// </summary>
    public Dictionary<string, AorAirspaceEdit> AirspaceEdits { get; set; } = new();
}

/// <summary>Quel che chi aggiorna il documento scrive su un volume dell'AIP: la classe (quando il file non la dà, o la
/// dà sbagliata) e una nota libera. Null = niente di scritto.</summary>
public sealed class AorAirspaceEdit
{
    public string? Class { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Una riga della tabella «spazi aerei» sotto l'AoR: un volume dell'AIP agganciato a un settore del documento.
/// <paramref name="FileClass"/> è la classe del KMZ (quasi sempre null sui CTR), <paramref name="EditedClass"/> quella
/// scritta a mano; si mostra <see cref="Class"/>. <paramref name="Callsign"/> = il settore che lo disegna (il primo, se
/// sono più d'uno): dà il colore al pallino che accende e spegne lo spazio sulla mappa. Null negli snapshot di 1.31.0.
/// </summary>
public sealed record AorAirspaceRow(
    string VolumeKey, string Name, string BaseRaw, string TopRaw,
    string? FileClass, string? EditedClass, string? Note, string? Callsign = null)
{
    /// <summary>La classe da mostrare: quella scritta a mano vince su quella del file.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Class => EditedClass ?? FileClass;
}

/// <summary>Normalizza la personalizzazione AoR prima del salvataggio: callsign trimmati/dedup, colori solo per
/// callsign non vuoti con hex non vuoto. Condiviso tra il salvataggio ACC e APP.</summary>
internal static class AorCustomizationCleaner
{
    public static AorExtraShapes Clean(AorExtraShapes? data)
    {
        data ??= new AorExtraShapes();
        var callsigns = (data.Callsigns ?? new())
            .Select(c => (c ?? "").Trim()).Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in data.Colors ?? new())
        {
            var cs = (kv.Key ?? "").Trim();
            var hex = (kv.Value ?? "").Trim();
            if (cs.Length > 0 && hex.Length > 0) colors[cs] = hex;
        }
        var edits = new Dictionary<string, AorAirspaceEdit>(StringComparer.Ordinal);
        foreach (var kv in data.AirspaceEdits ?? new())
        {
            var key = (kv.Key ?? "").Trim();
            var cls = CleanClass(kv.Value?.Class);
            var note = string.IsNullOrWhiteSpace(kv.Value?.Note) ? null : kv.Value!.Note!.Trim();
            if (key.Length > 0 && (cls is not null || note is not null))
                edits[key] = new AorAirspaceEdit { Class = cls, Note = note };
        }
        return new AorExtraShapes { Callsigns = callsigns, Colors = colors, AirspaceEdits = edits };
    }

    /// <summary>Vero se non resta niente da salvare: la sezione torna senza JSON.</summary>
    public static bool IsEmpty(AorExtraShapes clean) =>
        clean.Callsigns.Count == 0 && clean.Colors.Count == 0 && clean.AirspaceEdits.Count == 0;

    /// <summary>Classe ICAO di spazio aereo: una lettera A…G, maiuscola. Qualunque altra cosa = nessuna classe.</summary>
    internal static string? CleanClass(string? value)
    {
        var v = (value ?? "").Trim().ToUpperInvariant();
        return v.Length == 1 && v[0] is >= 'A' and <= 'G' ? v : null;
    }
}

/// <summary>Radice di un albero di settori CTR dell'ACC (una vIPI per albero). Callsign + nome del CTR radice.</summary>
public sealed record AccTreeRoot(string Callsign, string Name);

/// <summary>Area speciale selezionabile (picker editor). <see cref="Centers"/> = ACC che la elencano — sono più di
/// uno per le aree condivise fra centri (il picker cross-ACC li mostra e ci filtra sopra).</summary>
public sealed record SpecialAreaPick(
    string IvaoId, string Name, string? Type, int? MinimumAlt, int? MaximumAlt, IReadOnlyList<string> Centers)
{
    /// <summary>Enti in forma leggibile, per la riga del picker.</summary>
    public string CentersText => string.Join(" · ", Centers);
}

/// <summary>Selezione delle aree regolamentate di un blocco vIPI ACC. Le aree del <b>proprio</b> ACC sono in
/// <see cref="OwnAuto"/> (tutte, dinamiche: seguono gli import) finché lo staff non passa a manuale scegliendo un
/// sottoinsieme in <see cref="OwnIds"/>. <see cref="ExtraIds"/> sono aree di <b>altri</b> ACC aggiunte a mano
/// (indipendenti dal modo auto/manuale delle proprie). Il modo auto vale solo per il blocco Aerovia.</summary>
public sealed class RegulatedSelection
{
    public bool OwnAuto { get; set; } = true;
    public List<string> OwnIds { get; set; } = new();
    public List<string> ExtraIds { get; set; } = new();
}

/// <summary>Area speciale grezza dal DB (per la proiezione nel viewer). Shape = JSON grezzo.</summary>
/// <param name="Range">Poligono di tiro (<i>weapon range</i>): il flag <c>range</c> dell'import IVAO.</param>
public sealed record SpecialAreaDetail(
    string IvaoId, string Name, string? Type, string? Description, string? ActivationDetails,
    int? MinimumAlt, int? MaximumAlt, string? RegionMapPolygon, bool Range = false);

/// <summary>Area speciale attaccata a una vIPI, risolta per il viewer: metadati + shape proiettata (null = assente).</summary>
/// <param name="Range">
/// Poligono di tiro (<i>weapon range</i>), dal flag <c>range</c> dell'import IVAO.
///
/// <para>⚠️ È una proprietà <b>trasversale al tipo</b>, non un tipo in più: nei dati veri i poligoni di tiro
/// stanno sotto R, D e TRA insieme. Perciò a schermo si dice con un SEGNO PROPRIO (colore e riga marcata) e
/// non aggiungendo una voce alla tavolozza dei tipi.</para>
/// </param>
public sealed record AccSpecialAreaView(
    string IvaoId, string Name, string? Type, string? Description, string? ActivationDetails,
    int? MinimumAlt, int? MaximumAlt, AppAorPolygon? Shape, bool Range = false);

/// <summary>Flussi verso un aeroporto (foglia dell'albero): arrivi + partenze separati.</summary>
public sealed record AccAirportFlows(string AirportLabel, IReadOnlyList<AppCoordRow> Arrivals, IReadOnlyList<AppCoordRow> Departures);

/// <summary>Flussi senza aeroporto (sorvoli/VFR/altro) sotto un ACC, raggruppati per etichetta di tipo.</summary>
public sealed record AccExtraFlows(string KindLabel, IReadOnlyList<AppCoordRow> Rows);

/// <summary>Aeroporti di un ACC (secondo livello): gli aeroporti a cui il settore trasferisce (arrivi/partenze)
/// + i flussi senza aeroporto (<see cref="Extras"/>: sorvoli/VFR/altro).</summary>
public sealed record AccAccAirports(string AccLabel, IReadOnlyList<AccAirportFlows> Airports, IReadOnlyList<AccExtraFlows> Extras);

/// <summary>Coordinamenti di un settore del blocco (primo livello, es. «NE»): ACC → Aeroporto → Arrivi/Partenze.</summary>
public sealed record AccSectorApps(string SectorLabel, IReadOnlyList<AccAccAirports> Accs);

/// <summary>Coordinamenti derivati di un blocco: un unico albero Settore → ACC → Aeroporto → Arrivi/Partenze.</summary>
public sealed class AccCoordination
{
    public required IReadOnlyList<AccSectorApps> Sectors { get; init; }

    public static AccCoordination Empty { get; } = new() { Sectors = Array.Empty<AccSectorApps>() };
}

/// <summary>AoR di un singolo settore del blocco: callsign + nome + colore + poligoni. Anello toggleabile in mappa.
/// <paramref name="LowerFl"/>/<paramref name="UpperFl"/> = banda FL per l'estrusione 3D (normalizzata via
/// <see cref="Vipi.Application.Aor.AorFlBand"/>; null = non disponibile → il viewer 3D usa GND/UNL di default).
/// <para><paramref name="Label"/> = testo della chip quando il <paramref name="Callsign"/> non è da mostrare.
/// Per i settori è null e la chip dice il callsign, che è il nome con cui li si chiama. Serve alle <b>aree
/// regolamentate</b>, che riusano questa mappa (<see cref="Vipi.Application.Aor.RegulatedAreasMap"/>): lì il
/// «callsign» è l'id IVAO, cioè un numero, e la chip deve dire il nome dell'area.</para></summary>
/// <param name="Dashed">La forma si disegna TRATTEGGIATA e quasi senza riempimento. Serve al convertitore di
/// coordinate, che sovrappone alla forma di partenza quella riconvertita: senza il tratteggio la seconda
/// coprirebbe la prima e il confronto non direbbe niente.</param>
public sealed record AccSectorAor(
    string Callsign, string Name, string Color, IReadOnlyList<AppAorPolygon> Polygons,
    int? LowerFl = null, int? UpperFl = null, string? Label = null, bool Dashed = false,
    string? BandText = null);

/// <summary>Selezione di configurazione per la mappa: quali settori accendere.</summary>
public sealed record AccConfigSelection(string Key, string Name, IReadOnlyList<string> OpenCallsigns);

/// <summary>Riga tabella accorpamento derivata: settore unificato (aperto) + settori assorbiti + CP/Range manuali.
/// Settore unificato e assorbiti sono espressi come <b>callsign</b> (es. <c>LIRR_NE_CTR</c>), non come nome.</summary>
public sealed record AccConfigTableRow(
    string UnifiedCallsign, IReadOnlyList<string> Absorbed, string? CenterPoint, string? Range);

/// <summary>Tabella accorpamento di una configurazione (derivata via AorService).</summary>
public sealed record AccConfigTableView(string ConfigKey, string ConfigName, IReadOnlyList<AccConfigTableRow> Rows);

/// <summary>Vista AoR del blocco: settori (anelli toggleabili) + configurazioni selezionabili. Una sola mappa.</summary>
/// <param name="Airspaces">Righe della tabella «spazi aerei» sotto la mappa: i volumi dell'AIP agganciati ai settori del
/// documento. Null = nessuno (e negli snapshot di prima del 17-set-2026, dove il campo non c'è).</param>
public sealed record AccAorView(
    IReadOnlyList<AccSectorAor> Sectors, IReadOnlyList<AccConfigSelection> Configs,
    IReadOnlyList<AorAirspaceRow>? Airspaces = null)
{
    public static AccAorView Empty { get; } = new(Array.Empty<AccSectorAor>(), Array.Empty<AccConfigSelection>());

    /// <summary>Le righe della tabella, mai null.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<AorAirspaceRow> AirspaceRows => Airspaces ?? Array.Empty<AorAirspaceRow>();
}
