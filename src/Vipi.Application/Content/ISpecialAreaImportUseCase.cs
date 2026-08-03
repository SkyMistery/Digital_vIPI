namespace Vipi.Application.Content;

/// <summary>
/// Core import aree speciali/regolamentate dalla sorgente, per tutti gli ACC: corpo unico condiviso da
/// manual (con authz, via <see cref="AccAdminService.ImportFromSourceAsync"/>) e auto (hosted service).
/// Upsert per IvaoId + prune per-ACC; isolamento errori per-ACC (un ACC fallito non blocca gli altri).
/// Doc refactor 02 §4.2. Nessun controllo di autorizzazione qui: lo applica solo il chiamante manual.
/// Rispetta la policy di import globale (<c>ImportCategory.SpecialAreas</c>): categoria esclusa = nessuna fetch
/// e nessun prune, le aree già in archivio restano. Il gate è qui perché questo corpo è condiviso auto/manual.
/// </summary>
public interface ISpecialAreaImportUseCase
{
    /// <summary>Esegue import + prune su ogni ACC <b>abilitato</b> (<c>Acc.SpecialAreasEnabled</c>). Ritorna i
    /// conteggi aggregati e gli ACC saltati.</summary>
    Task<SpecialAreaImportResult> RunAsync(CancellationToken ct = default);

    /// <summary>
    /// Import + prune di un solo ACC, <b>ignorando</b> il suo flag: è il primo scarico manuale con cui l'admin
    /// accende un ente estero (da lì in poi entra nel giro periodico). La policy globale resta vincolante.
    /// </summary>
    Task<SpecialAreaImportResult> RunForAccAsync(string accCode, CancellationToken ct = default);
}
