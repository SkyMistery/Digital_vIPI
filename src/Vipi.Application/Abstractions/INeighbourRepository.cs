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

/// <summary>Proprietario catalogato di un callsign (ACC di appartenenza + se nascosto), per il guard anti-collisione
/// dell'aggiunta manuale di settori esteri: un callsign non può essere spostato sotto un altro ACC.</summary>
public sealed record SectorOwner(string AccCode, bool IsHidden);

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
    /// <param name="manuale">
    /// Vero quando le righe le sta aggiungendo una <b>persona</b> (pagina Confinanti) e non il giro
    /// automatico. Marca le righe nuove come tali: sono le uniche che la sorgente non ristampa mai, e senza
    /// quel segno il controllo del timbro le prenderebbe per «sparite dalla sorgente» già il primo giorno.
    /// </param>
    Task PersistForeignCatalogAsync(IReadOnlyList<ForeignAccImport> accs, bool manuale = false, CancellationToken ct = default);

    /// <summary>Fa upsert dei candidati calcolati. Aggiorna solo le righe in stato Pending (non tocca
    /// Confirmed/Rejected né i poligoni impostati a mano). Ritorna (creati, aggiornati).</summary>
    Task<(int Created, int Updated)> UpsertCandidatesAsync(IReadOnlyList<NeighbourCandidateUpsert> items, CancellationToken ct = default);

    Task<IReadOnlyList<NeighbourCandidate>> ListCandidatesAsync(CancellationToken ct = default);
    Task<NeighbourCandidate?> GetAsync(int id, CancellationToken ct = default);
    Task SetStatusAsync(int id, NeighbourCandidateStatus status, CancellationToken ct = default);
    Task SetPolygonAsync(int id, string? regionMapPolygon, CancellationToken ct = default);

    /// <summary>Aggiunge a mano una coppia confinante (fallback: IVAO senza dati/poligono). Chiave (Home,Foreign) unica.</summary>
    Task<int> AddManualAsync(NeighbourCandidateUpsert item, CancellationToken ct = default);

    /// <summary>Trova l'ACC proprietario di un callsign già catalogato (cerca in <c>AccSector</c> e <c>AirportSector</c>);
    /// null se il callsign è libero. Usato dal guard dell'aggiunta manuale di settori esteri (no hijack/duplicati).</summary>
    Task<SectorOwner?> FindSectorOwnerAsync(string callsign, CancellationToken ct = default);

    /// <summary>Materializza ACC+settore esteri e crea la vLOA bilaterale (Home=settore radice domestico,
    /// Neighbour=settore estero). Idempotente: se la coppia ha già una vLOA, ritorna quella. Ritorna l'Id doc.</summary>
    Task<int> MaterializeAndCreateVloaAsync(int candidateId, CancellationToken ct = default);
}
