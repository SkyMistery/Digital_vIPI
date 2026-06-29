namespace Vipi.Application.Abstractions;

/// <summary>
/// Sincronizza i <c>Sector</c> operativi (proiezione) dai cataloghi importati (<c>AccSector</c>/<c>AirportSector</c>),
/// fonte autoritativa unica (Round 20, SPEC §9.12). Idempotente, di sistema (nessuna autorizzazione utente):
/// va invocata al termine degli import (ACC/settori aeroporto) e dopo le modifiche alla gerarchia per callsign.
/// </summary>
/// <remarks>
/// - Upsert per <c>Callsign</c> (= <c>ComposePosition</c>): preserva <c>Sector.Id</c> e i legami editoriali
///   (<c>DocumentId</c>/<c>IsPrimary</c>/<c>FeaturedRank</c>), così le FK documento non si rompono.
/// - Deriva <c>Type</c>/<c>Kind</c>/<c>AccId</c>/<c>DefaultFrequency</c>/<c>AirportId</c> dai cataloghi e
///   <c>ParentSectorId</c> dal <c>ParentCallsign</c> del catalogo (gerarchia per callsign, cross-ACC).
/// - I settori proiettati spariti o nascosti nel catalogo vengono disattivati (<c>IsActive=false</c>), non cancellati.
/// - I settori NON proiettati (seed/manuali, <c>IsProjected=false</c>) non vengono mai toccati.
/// </remarks>
public interface ISectorProjectionService
{
    /// <summary>Esegue la sincronizzazione. Ritorna il numero di settori proiettati creati o aggiornati.</summary>
    Task<int> SyncFromCatalogsAsync(CancellationToken ct = default);
}
