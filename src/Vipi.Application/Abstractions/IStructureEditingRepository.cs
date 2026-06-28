using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta di scrittura dell'anagrafica/struttura di una FIR: FIR, settori (entità unificata ex Position+Sector),
/// contenimento (padre), frequenze. Impl. EF in Infrastructure. I metodi prendono il <c>firCode</c> per
/// l'autorizzazione e verificano l'appartenenza alla FIR. Regole di unificazione e padre in <see cref="ITopologyEditingRepository"/>.
/// </summary>
public interface IStructureEditingRepository
{
    Task<IReadOnlyList<FirRow>> ListFirsAsync(CancellationToken ct = default);
    Task<bool> FirExistsAsync(string code, CancellationToken ct = default);
    Task<int> CreateFirAsync(string code, string name, string countryPrefix, CancellationToken ct = default);
    /// <summary>Elimina una FIR solo se senza settori/documenti. Lancia se non vuota.</summary>
    Task DeleteFirAsync(string firCode, CancellationToken ct = default);

    Task<StructureData?> LoadAsync(string firCode, CancellationToken ct = default);

    // --- Aeroporti (entità di prima classe sotto una FIR) ---
    Task<bool> AirportIcaoExistsAsync(string icao, CancellationToken ct = default);
    Task<int> CreateAirportAsync(string firCode, string icao, string name, CancellationToken ct = default);
    /// <summary>Elimina un aeroporto solo se nessun settore vi punta. Lancia se referenziato.</summary>
    Task DeleteAirportAsync(string firCode, int airportId, CancellationToken ct = default);
    /// <summary>Sposta un aeroporto (e i suoi settori) sotto un'altra FIR. Stacca il padre dei settori spostati se fuori FIR.</summary>
    Task MoveAirportAsync(int airportId, string targetFirCode, CancellationToken ct = default);

    /// <summary>Tutti gli aeroporti assegnati a una FIR (cross-FIR), per la pagina di gestione.</summary>
    Task<IReadOnlyList<AirportAdminRow>> ListAllAirportsAsync(CancellationToken ct = default);
    /// <summary>Tutti i settori (id+callsign+FIR), per i menu della gestione aeroporti.</summary>
    Task<IReadOnlyList<SectorBriefRow>> ListAllSectorsAsync(CancellationToken ct = default);

    /// <summary>
    /// Crea in blocco gli aeroporti candidati la cui <c>FirCode</c> corrisponde a una FIR esistente e il cui
    /// ICAO non è ancora assegnato. Esistenza FIR/ICAO verificata server-side (autorità DB). Ritorna i creati.
    /// </summary>
    Task<int> AutoAssignAirportsAsync(
        IReadOnlyList<(string FirCode, string Icao, string Name)> candidates, CancellationToken ct = default);

    /// <summary>
    /// Crea i settori d'aeroporto mancanti (DEL/GND/TWR con contenimento top-down) dalle <paramref name="positions"/>
    /// per un aeroporto già assegnato a una FIR. Idempotente sui settori esistenti. Ritorna (creati, aeroporto trovato).
    /// La generazione del documento e del profilo è demandata a <see cref="IAirportProfileRepository"/>.
    /// </summary>
    Task<(int Created, bool AirportFound)> EnsureAirportSectorsAsync(
        string icao,
        IReadOnlyList<(SectorType Type, string Callsign, string? Frequency)> positions,
        CancellationToken ct = default);

    /// <summary>Imposta gli aeroporti "in evidenza" (FeaturedRank 1..N nell'ordine dato) della FIR, azzerando gli altri.</summary>
    Task SetFeaturedAirportsAsync(string firCode, IReadOnlyList<int> orderedAirportIds, CancellationToken ct = default);
    /// <summary>Imposta gli APP "in evidenza" (FeaturedRank 1..N nell'ordine dato) della FIR, azzerando gli altri APP.</summary>
    Task SetFeaturedAppsAsync(string firCode, IReadOnlyList<int> orderedAppSectorIds, CancellationToken ct = default);
    /// <summary>Imposta le vLOA "in evidenza" (FeaturedRank 1..N nell'ordine dato) della FIR, azzerando le altre vLOA.</summary>
    Task SetFeaturedVloasAsync(string firCode, IReadOnlyList<int> orderedVloaDocIds, CancellationToken ct = default);

    Task<bool> CallsignExistsAsync(string callsign, CancellationToken ct = default);
    Task<int> AddSectorAsync(string firCode, string callsign, SectorType type, SectorKind kind, string name,
        string? defaultFrequency, int coverageOrder, ApproachKind? approachKind, int? parentSectorId,
        int? airportId, CancellationToken ct = default);
    Task DeleteSectorAsync(string firCode, int sectorId, CancellationToken ct = default);

    Task<int> AddFrequencyAsync(string firCode, int sectorId, string label, string callsign,
        string frequencyMhz, bool isPrimary, CancellationToken ct = default);
    Task DeleteFrequencyAsync(string firCode, int frequencyId, CancellationToken ct = default);
}
