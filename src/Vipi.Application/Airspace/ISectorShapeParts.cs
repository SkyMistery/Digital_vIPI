using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>
/// Un pezzo di forma come lo si scrive e come lo si legge: un anello <b>e le sue quote</b>.
///
/// <para>⚠️ Le quote non sono un parametro accanto: stanno <b>dentro</b>. È così che «laterale da una fonte,
/// verticale da un'altra» smette di essere una disciplina e diventa una cosa che non si può scrivere.</para>
/// </summary>
public sealed record ShapePart(
    string PolygonJson,
    int? BaseFeet, int? TopFeet,
    AirspaceDatum BaseDatum, AirspaceDatum TopDatum,
    string BaseRaw, string TopRaw,
    string? SourceRef = null);

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
