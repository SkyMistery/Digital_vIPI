using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta di scrittura dell'anagrafica/struttura di una ACC: ACC, settori (entità unificata ex Position+Sector),
/// contenimento (padre), frequenze. Impl. EF in Infrastructure. I metodi prendono il <c>accCode</c> per
/// l'autorizzazione e verificano l'appartenenza alla ACC.
/// </summary>
public interface IStructureEditingRepository
{
    Task<IReadOnlyList<AccRow>> ListAccsAsync(CancellationToken ct = default);
    Task<bool> AccExistsAsync(string code, CancellationToken ct = default);
    Task<int> CreateAccAsync(string code, string name, string countryPrefix, CancellationToken ct = default);
    /// <summary>Elimina una ACC solo se senza settori/documenti. Lancia se non vuota.</summary>
    Task DeleteAccAsync(string accCode, CancellationToken ct = default);

    Task<StructureData?> LoadAsync(string accCode, CancellationToken ct = default);

    // --- Aeroporti (entità di prima classe sotto una ACC) ---
    Task<bool> AirportIcaoExistsAsync(string icao, CancellationToken ct = default);
    Task<int> CreateAirportAsync(string accCode, string icao, string name, CancellationToken ct = default);
    /// <summary>Elimina un aeroporto solo se nessun settore vi punta. Lancia se referenziato.</summary>
    Task DeleteAirportAsync(string accCode, int airportId, CancellationToken ct = default);
    /// <summary>Sposta un aeroporto (e i suoi settori) sotto un'altra ACC. Stacca il padre dei settori spostati se fuori ACC.</summary>
    Task MoveAirportAsync(int airportId, string targetAccCode, CancellationToken ct = default);

    /// <summary>Tutti gli aeroporti assegnati a una ACC (cross-ACC), per la pagina di gestione.</summary>
    Task<IReadOnlyList<AirportAdminRow>> ListAllAirportsAsync(CancellationToken ct = default);

    /// <summary>Nasconde/mostra un aeroporto: la sua pagina pubblica e l'elenco non lo mostrano più (resta nel DB).</summary>
    Task SetAirportHiddenAsync(string accCode, int airportId, bool hidden, CancellationToken ct = default);
    /// <summary>Tutti i settori (id+callsign+ACC), per i menu della gestione aeroporti.</summary>
    Task<IReadOnlyList<SectorBriefRow>> ListAllSectorsAsync(CancellationToken ct = default);

    /// <summary>Tutti i settori attivi in vista globale (cross-ACC) col prefisso nazione, l'albero e il documento, per il picker di «Nuovo documento».</summary>
    Task<IReadOnlyList<GlobalSectorRow>> ListSectorNodesAsync(CancellationToken ct = default);

    /// <summary>
    /// Crea in blocco gli aeroporti candidati la cui <c>AccCode</c> corrisponde a una ACC esistente e il cui
    /// ICAO non è ancora assegnato. Esistenza ACC/ICAO verificata server-side (autorità DB). Ritorna gli ICAO creati.
    /// </summary>
    Task<IReadOnlyList<string>> AutoAssignAirportsAsync(
        IReadOnlyList<(string AccCode, string Icao, string Name)> candidates, CancellationToken ct = default);

    /// <summary>
    /// Crea i settori d'aeroporto mancanti (DEL/GND/TWR con contenimento top-down) dalle <paramref name="positions"/>
    /// per un aeroporto già assegnato a una ACC. Idempotente sui settori esistenti. Ritorna (creati, aeroporto trovato).
    /// La generazione del documento e del profilo è demandata a <see cref="IAirportRepository"/>.
    /// </summary>
    Task<(int Created, bool AirportFound)> EnsureAirportSectorsAsync(
        string icao,
        IReadOnlyList<(SectorType Type, string Callsign, string? Frequency)> positions,
        CancellationToken ct = default);

    /// <summary>Imposta gli aeroporti "in evidenza" (FeaturedRank 1..N nell'ordine dato) della ACC, azzerando gli altri.</summary>
    Task SetFeaturedAirportsAsync(string accCode, IReadOnlyList<int> orderedAirportIds, CancellationToken ct = default);
    /// <summary>Imposta gli APP "in evidenza" (FeaturedRank 1..N nell'ordine dato) della ACC, azzerando gli altri APP.</summary>
    Task SetFeaturedAppsAsync(string accCode, IReadOnlyList<int> orderedAppSectorIds, CancellationToken ct = default);
    /// <summary>Imposta le vLOA "in evidenza" (FeaturedRank 1..N nell'ordine dato) della ACC, azzerando le altre vLOA.</summary>
    Task SetFeaturedVloasAsync(string accCode, IReadOnlyList<int> orderedVloaDocIds, CancellationToken ct = default);

    Task<bool> CallsignExistsAsync(string callsign, CancellationToken ct = default);
    Task<int> AddSectorAsync(string accCode, string callsign, SectorType type, SectorKind kind, string name,
        string? defaultFrequency, int coverageOrder, ApproachKind? approachKind, int? parentSectorId,
        int? airportId, CancellationToken ct = default);
    Task DeleteSectorAsync(string accCode, int sectorId, CancellationToken ct = default);

    /// <summary>Imposta la frequenza del settore (Sector.DefaultFrequency); vuoto/null = nessuna. Rifiuta i settori
    /// PROIETTATI (fonte = catalogo, sola lettura): il sync ne sovrascriverebbe l'edit. Solo settori seed/manuali.</summary>
    Task SetSectorFrequencyAsync(string accCode, int sectorId, string? frequencyMhz, CancellationToken ct = default);
}
