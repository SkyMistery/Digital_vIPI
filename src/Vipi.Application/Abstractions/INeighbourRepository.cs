using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Settore ACC domestico (della divisione) col suo poligono shape, per il calcolo di adiacenza.</summary>
public sealed record DomesticSectorPoly(string CenterId, string ComposePosition, string RegionMapPolygon);

/// <summary>Dati per fare upsert di una coppia ACC confinante candidata (solo le righe Pending vengono aggiornate).</summary>
public sealed record NeighbourCandidateUpsert(
    string HomeAccCode, string ForeignAccCode, string ForeignAccName, string CountryId,
    string ForeignRootCallsign, string? RegionMapPolygon, double? MinDistanceNm, int AdjacentSectorCount,
    IReadOnlyList<string>? AdjacentHomeCallsigns = null, IReadOnlyList<string>? AdjacentForeignCallsigns = null);

/// <summary>Catalogo di un ACC estero confinante da persistire (Acc + subcenter confinanti), per l'import.</summary>
public sealed record ForeignAccImport(string Code, string Name, IReadOnlyList<SourceSubcenter> Subcenters);

/// <summary>
/// Persistenza dei candidati vLOA confinanti: legge i settori ACC domestici (con poligono) per l'adiacenza,
/// fa staging delle coppie proposte, e — alla conferma — materializza ACC/settore esteri e genera la vLOA.
/// </summary>
public interface INeighbourRepository
{
    /// <summary>Settori ACC domestici (non esteri) attivi con poligono (catalogo <c>AccSectors</c>), per l'adiacenza.</summary>
    Task<IReadOnlyList<DomesticSectorPoly>> ListDomesticSectorPolygonsAsync(CancellationToken ct = default);

    /// <summary>Codici degli ACC domestici (Acc non esteri), per distinguere «casa» da estero.</summary>
    Task<IReadOnlyList<string>> ListDomesticAccCodesAsync(CancellationToken ct = default);

    /// <summary>Persiste il catalogo degli ACC esteri confinanti: upsert <c>Acc</c> (IsForeign=true) + i loro subcenter
    /// come <c>AccSector</c> (preservando ParentCallsign/IsHidden impostati dall'admin). Idempotente per chiave naturale.</summary>
    Task PersistForeignCatalogAsync(IReadOnlyList<ForeignAccImport> accs, CancellationToken ct = default);

    /// <summary>Fa upsert dei candidati calcolati. Aggiorna solo le righe in stato Pending (non tocca
    /// Confirmed/Rejected né i poligoni impostati a mano). Ritorna (creati, aggiornati).</summary>
    Task<(int Created, int Updated)> UpsertCandidatesAsync(IReadOnlyList<NeighbourCandidateUpsert> items, CancellationToken ct = default);

    Task<IReadOnlyList<NeighbourCandidate>> ListCandidatesAsync(CancellationToken ct = default);
    Task<NeighbourCandidate?> GetAsync(int id, CancellationToken ct = default);
    Task SetStatusAsync(int id, NeighbourCandidateStatus status, CancellationToken ct = default);
    Task SetPolygonAsync(int id, string? regionMapPolygon, CancellationToken ct = default);

    /// <summary>Aggiunge a mano una coppia confinante (fallback: IVAO senza dati/poligono). Chiave (Home,Foreign) unica.</summary>
    Task<int> AddManualAsync(NeighbourCandidateUpsert item, CancellationToken ct = default);

    /// <summary>Materializza ACC+settore esteri e crea la vLOA bilaterale (Home=settore radice domestico,
    /// Neighbour=settore estero). Idempotente: se la coppia ha già una vLOA, ritorna quella. Ritorna l'Id doc.</summary>
    Task<int> MaterializeAndCreateVloaAsync(int candidateId, CancellationToken ct = default);
}
