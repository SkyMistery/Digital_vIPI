namespace Vipi.Application.Content;

/// <summary>
/// Core import aree speciali/regolamentate dalla sorgente, per tutti gli ACC: corpo unico condiviso da
/// manual (con authz, via <see cref="AccAdminService.ImportFromSourceAsync"/>) e auto (hosted service).
/// Upsert per IvaoId + prune per-ACC; isolamento errori per-ACC (un ACC fallito non blocca gli altri).
/// Doc refactor 02 §4.2. Nessun controllo di autorizzazione qui: lo applica solo il chiamante manual.
/// </summary>
public interface ISpecialAreaImportUseCase
{
    /// <summary>Esegue import + prune su ogni ACC. Ritorna i conteggi aggregati e gli ACC saltati.</summary>
    Task<SpecialAreaImportResult> RunAsync(CancellationToken ct = default);
}
