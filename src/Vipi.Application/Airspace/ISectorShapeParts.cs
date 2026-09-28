using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>
/// Un pezzo di forma come lo si scrive e come lo si legge: un anello <b>e le sue quote</b>.
///
/// <para>⚠️ Le quote non sono un parametro accanto: stanno <b>dentro</b>. È così che «laterale da una fonte,
/// verticale da un'altra» smette di essere una disciplina e diventa una cosa che non si può scrivere.</para>
///
/// <para><paramref name="Name"/> e <paramref name="AirspaceClass"/> li porta solo un pezzo dell'<b>aggancio</b>
/// all'AIP: sono le righe della tabella sotto l'AoR (carta 2026-09-17-tabella-spazi-aerei-nell-aor.md). Così
/// la tabella dice esattamente quel che la mappa disegna, senza una seconda lettura degli agganci.</para>
/// </summary>
public sealed record ShapePart(
    string PolygonJson,
    int? BaseFeet, int? TopFeet,
    AirspaceDatum BaseDatum, AirspaceDatum TopDatum,
    string BaseRaw, string TopRaw,
    string? SourceRef = null,
    string? Name = null, string? AirspaceClass = null)
{
    /// <summary>
    /// Base e tetto <b>sul livello del mare</b>: le quote <c>AGL</c> si alzano dell'elevazione dello scalo, le altre
    /// restano come sono. Senza elevazione l'AGL vale AMSL, come prima.
    /// <para>🔴 U-217 (revisione totale 3, scelta del committente del 28 settembre 2026): le ATZ in AGL si leggevano
    /// come AMSL sia nell'attribuzione del traffico sia nel 3D — una torre «GND–1500 FT AGL» su un campo a 1050 ft
    /// rivendicava il cielo fino a 1500 ft sul mare, cioè 450 sopra la pista. <b>Una regola sola</b> per le
    /// statistiche e per la mappa.</para>
    /// </summary>
    public (int? Base, int? Top) QuoteAmsl(int? elevazioneFt) =>
        (Alza(BaseFeet, BaseDatum, elevazioneFt), Alza(TopFeet, TopDatum, elevazioneFt));

    private static int? Alza(int? piedi, AirspaceDatum datum, int? elevazioneFt) =>
        piedi is int p && datum == AirspaceDatum.Agl && elevazioneFt is int e ? p + e : piedi;
}

/// <summary>
/// Esito di una scrittura. <paramref name="SourceSilent"/> = la sorgente non ha detto niente (elenco vuoto):
/// <b>non si è cancellato nulla</b>, ed è un esito normale, non un errore.
/// </summary>
/// <param name="Written">Quanti pezzi sono ora in archivio per quella (fonte, stato).</param>
public sealed record ShapePartsWriteResult(int Written, bool SourceSilent)
{
    public static ShapePartsWriteResult Silent { get; } = new(0, true);
}

/// <summary>
/// L'archivio dei pezzi di forma di un settore, per fonte.
///
/// <para>⚠️ <b>LA REGOLA D'ORO STA NELLA FIRMA, non in un commento.</b> Ogni metodo che tocca l'archivio
/// prende una <see cref="ShapeSource"/> obbligatoria, e cancella <b>solo dentro quella</b>: non esiste, e non
/// deve nascere, un metodo che cancelli «i pezzi di un settore» senza dire di quale fonte. È ciò che rende
/// l'aggancio all'AIP reversibile — i pezzi di IVAO restano in archivio mentre l'AIP è attivo, quindi lo
/// sgancio non ha niente da ri-importare — e il giorno che la regola cadesse, l'unbind si romperebbe
/// <b>in silenzio</b>. Chi vuole violarla deve scrivere un metodo nuovo, che è un gesto che si vede in
/// revisione. Carta <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c> §3d.</para>
///
/// <para>⚠️ <b>L'assenza non cancella.</b> Un elenco vuoto vuol dire «la sorgente non ha parlato» e lascia
/// tutto com'era: è la lezione del 26 agosto 2026, quando gli upsert scrissero il <c>[]</c> della sorgente
/// sopra le shape e azzerarono 83 poligoni su 83. Svuotare è un gesto <b>separato ed esplicito</b>
/// (<see cref="ClearPartsAsync"/>), che chiamano solo lo sgancio e la pagina che elimina.</para>
/// </summary>
public interface ISectorShapeParts
{
    /// <summary>I pezzi di una fonte per un settore, in ordine di disegno. Vuoto = quella fonte non ha scritto.</summary>
    Task<IReadOnlyList<ShapePart>> ListAsync(
        SourceCatalog catalog, int sectorId, ShapeSource source, ShapePartState state,
        CancellationToken ct = default);

    /// <summary>
    /// I pezzi <b>in vigore</b> di più callsign in un colpo solo: è la lettura che serve al risolutore, e
    /// quella per cui esiste l'indice <c>(Callsign, State)</c>. Chiave = callsign maiuscolo.
    ///
    /// <para>Se per uno stesso settore ci fossero pezzi di più fonti, vince <c>Sectorfile</c>, poi
    /// <c>Source</c>, poi <c>Aip</c> (l'ATZ automatica), poi <c>Synthetic</c>: gli stessi gradini del risolutore,
    /// dove un confine vero del catalogo sta sopra l'ATZ e il cerchio di ripiego perde contro tutti. Rovesciata il
    /// 16 settembre 2026 (carta 15 §4-bis): prima l'<c>Aip</c> stava in testa.</para>
    /// </summary>
    Task<IReadOnlyDictionary<string, (ShapeSource Source, IReadOnlyList<ShapePart> Parts)>> ListInForceByCallsignAsync(
        IReadOnlyList<string> callsigns, CancellationToken ct = default);

    /// <summary>
    /// Riscrive i pezzi di <b>una fonte sola</b>: l'elenco dato sostituisce quello di prima per quella
    /// coppia (fonte, stato), e <b>non tocca nessun'altra fonte</b>.
    ///
    /// <para>⚠️ <paramref name="parts"/> vuoto <b>non cancella</b>: torna <see cref="ShapePartsWriteResult.Silent"/>.</para>
    /// </summary>
    Task<ShapePartsWriteResult> ReplacePartsAsync(
        SourceCatalog catalog, int sectorId, string callsign, ShapeSource source, ShapePartState state,
        IReadOnlyList<ShapePart> parts, string? airacCycle = null, bool forcePublished = false,
        CancellationToken ct = default);

    /// <summary>
    /// Svuota i pezzi di <b>una fonte sola</b>. È il gesto esplicito: lo sgancio da un volume dell'AIP, o la
    /// pagina che elimina. Torna quanti pezzi ha tolto.
    /// </summary>
    Task<int> ClearPartsAsync(
        SourceCatalog catalog, int sectorId, ShapeSource source, ShapePartState? state = null,
        CancellationToken ct = default);
}
