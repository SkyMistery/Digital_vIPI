using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case di authoring dell'anagrafica/struttura FIR. La creazione di una FIR è admin-only (non esiste
/// ancora una FIR su cui dare grant); le altre scritture sono FIR-gated via <see cref="IEditAuthorizationService"/>.
/// Validazioni hard sugli input (univocità callsign/sectorKey, campi obbligatori). Riusa <see cref="ValidationException"/>.
/// </summary>
public interface IStructureEditingService
{
    Task<IReadOnlyList<FirRow>> ListFirsAsync(CancellationToken ct = default);
    Task<StructureData?> LoadAsync(string firCode, CancellationToken ct = default);

    Task<int> CreateFirAsync(string code, string name, string? countryPrefix, CancellationToken ct = default);
    Task DeleteFirAsync(string firCode, CancellationToken ct = default);

    Task<int> CreateAirportAsync(string firCode, string icao, string name, CancellationToken ct = default);
    Task DeleteAirportAsync(string firCode, int airportId, CancellationToken ct = default);
    Task MoveAirportAsync(int airportId, string fromFirCode, string targetFirCode, CancellationToken ct = default);

    /// <summary>Gestione aeroporti (admin): elenco cross-FIR + settori per i menu.</summary>
    Task<IReadOnlyList<AirportAdminRow>> ListAllAirportsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SectorBriefRow>> ListAllSectorsAsync(CancellationToken ct = default);

    /// <summary>
    /// Assegna automaticamente (admin) gli aeroporti dell'anagrafica IVAO la cui FIR di competenza
    /// (centerId) esiste già nel DB e che non sono ancora assegnati. Ritorna il numero di aeroporti creati.
    /// </summary>
    Task<int> AutoAssignKnownAirportsAsync(CancellationToken ct = default);

    /// <summary>
    /// Genera (admin) il documento di aeroporto per un ICAO già assegnato: scarica postazioni ATC + piste da IVAO,
    /// crea i settori DEL/GND/TWR (APP ignorato per ora) e il documento vIPI pubblicato. Idempotente.
    /// </summary>
    Task<AirportDocResult> GenerateAirportDocumentAsync(string icao, CancellationToken ct = default);

    Task<int> AddSectorAsync(string firCode, string callsign, SectorType type, SectorKind kind, string name,
        string? defaultFrequency, int coverageOrder, ApproachKind? approachKind, int? parentSectorId,
        int? airportId, CancellationToken ct = default);
    Task DeleteSectorAsync(string firCode, int sectorId, CancellationToken ct = default);

    /// <summary>Imposta gli aeroporti "in evidenza" della FIR (ordine = FeaturedRank 1..3) per la landing ACC.</summary>
    Task SetFeaturedAirportsAsync(string firCode, IReadOnlyList<int> orderedAirportIds, CancellationToken ct = default);
    /// <summary>Imposta gli APP "in evidenza" della FIR (ordine = FeaturedRank 1..3) per la landing ACC.</summary>
    Task SetFeaturedAppsAsync(string firCode, IReadOnlyList<int> orderedAppSectorIds, CancellationToken ct = default);
    /// <summary>Imposta le vLOA "in evidenza" della FIR (ordine = FeaturedRank 1..3) per la landing ACC.</summary>
    Task SetFeaturedVloasAsync(string firCode, IReadOnlyList<int> orderedVloaDocIds, CancellationToken ct = default);

    Task<int> AddFrequencyAsync(string firCode, int sectorId, string label, string callsign,
        string frequencyMhz, bool isPrimary, CancellationToken ct = default);
    Task DeleteFrequencyAsync(string firCode, int frequencyId, CancellationToken ct = default);
}

/// <inheritdoc cref="IStructureEditingService"/>
public sealed class StructureEditingService : IStructureEditingService
{
    private readonly IStructureEditingRepository _repo;
    private readonly IAirportProfileRepository _profile;
    private readonly IEditAuthorizationService _authz;
    private readonly IAirportDirectory _directory;
    private readonly IAirportDetailProvider _details;
    private readonly IImportPolicyStore _policy;

    public StructureEditingService(
        IStructureEditingRepository repo, IAirportProfileRepository profile, IEditAuthorizationService authz,
        IAirportDirectory directory, IAirportDetailProvider details, IImportPolicyStore policy)
    {
        _repo = repo;
        _profile = profile;
        _authz = authz;
        _directory = directory;
        _details = details;
        _policy = policy;
    }

    public Task<IReadOnlyList<FirRow>> ListFirsAsync(CancellationToken ct = default) => _repo.ListFirsAsync(ct);

    public Task<StructureData?> LoadAsync(string firCode, CancellationToken ct = default) => _repo.LoadAsync(firCode, ct);

    public async Task<int> CreateFirAsync(string code, string name, string? countryPrefix, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (code.Length is < 2 or > 8) throw new ValidationException("Codice FIR non valido (es. LIRR).");
        if (name.Length == 0) throw new ValidationException("Nome FIR obbligatorio.");
        if (await _repo.FirExistsAsync(code, ct)) throw new ValidationException($"FIR {code} già esistente.");
        var prefix = string.IsNullOrWhiteSpace(countryPrefix) ? code.Substring(0, 2) : countryPrefix.Trim().ToUpperInvariant();
        return await _repo.CreateFirAsync(code, name, prefix, ct);
    }

    public async Task DeleteFirAsync(string firCode, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.DeleteFirAsync(firCode, ct);
    }

    public async Task<int> CreateAirportAsync(string firCode, string icao, string name, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        icao = (icao ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (icao.Length != 4) throw new ValidationException("ICAO aeroporto non valido (4 lettere, es. LIRF).");
        if (name.Length == 0) throw new ValidationException("Nome aeroporto obbligatorio.");
        if (await _repo.AirportIcaoExistsAsync(icao, ct)) throw new ValidationException($"Aeroporto {icao} già esistente.");
        return await _repo.CreateAirportAsync(firCode, icao, name, ct);
    }

    public async Task DeleteAirportAsync(string firCode, int airportId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.DeleteAirportAsync(firCode, airportId, ct);
    }

    public async Task MoveAirportAsync(int airportId, string fromFirCode, string targetFirCode, CancellationToken ct = default)
    {
        // Spostamento cross-FIR: serve poter editare sia origine sia destinazione.
        await _authz.EnsureCanEditFirAsync(fromFirCode, ct);
        await _authz.EnsureCanEditFirAsync(targetFirCode, ct);
        await _repo.MoveAirportAsync(airportId, targetFirCode, ct);
    }

    public Task<IReadOnlyList<AirportAdminRow>> ListAllAirportsAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _repo.ListAllAirportsAsync(ct);
    }

    public Task<IReadOnlyList<SectorBriefRow>> ListAllSectorsAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _repo.ListAllSectorsAsync(ct);
    }

    public async Task<int> AutoAssignKnownAirportsAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        var ivao = await _directory.GetAirportsAsync(ct);
        var candidates = ivao
            .Where(a => !string.IsNullOrWhiteSpace(a.FirCode))
            .Select(a => (FirCode: a.FirCode!, a.Icao, a.Name))
            .ToList();
        return await _repo.AutoAssignAirportsAsync(candidates, ct);
    }

    public async Task<AirportDocResult> GenerateAirportDocumentAsync(string icao, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        icao = (icao ?? "").Trim().ToUpperInvariant();
        if (icao.Length != 4) throw new ValidationException("ICAO aeroporto non valido.");

        var positions = await _details.GetAtcPositionsAsync(icao, ct);
        var runways = await _details.GetRunwaysAsync(icao, ct);

        // Postazioni → settori d'aeroporto: solo DEL/GND/TWR (APP rimandato), niente duplicati di tipo.
        var sectors = positions
            .Select(p => (Type: ClassifySectorType(p.Callsign), p.Callsign, p.Frequency))
            .Where(x => x.Type is SectorType.Del or SectorType.Gnd or SectorType.Twr)
            .GroupBy(x => x.Type).Select(g => g.First())
            .ToList();

        // 1 — assicura i settori d'aeroporto (DEL/GND/TWR + fallback TWR).
        var (created, found) = await _repo.EnsureAirportSectorsAsync(icao, sectors, ct);
        if (!found) return new AirportDocResult(icao, false, 0, null, "Aeroporto non assegnato a una FIR.");

        // L'ATIS non è un settore controllabile: ne tengo solo la frequenza per la tabella Frequenze.
        var atisFreq = positions
            .FirstOrDefault(p => string.Equals(SuffixOf(p.Callsign), "ATIS", StringComparison.OrdinalIgnoreCase))?.Frequency;

        // Transition Altitude dall'anagrafica (centerId/transitionAltitude già scaricati con cache).
        int? ta = null;
        try
        {
            ta = (await _directory.GetAirportsAsync(ct))
                .FirstOrDefault(a => string.Equals(a.Icao, icao, StringComparison.OrdinalIgnoreCase))?.TransitionAltitude;
        }
        catch { /* anagrafica non disponibile: TA resta null, sezione da completare a mano */ }

        // 2 — merge nel profilo strutturato (preserva l'editoriale) e 3 — rigenera il documento.
        await _profile.MergeFromSourceAsync(icao, ta, atisFreq,
            runways.Select(r => (r.Ident, r.LengthM, r.Bearing)).ToList(), ct);
        var docId = await _profile.RebuildDocumentAsync(icao, ct);
        return new AirportDocResult(icao, true, created, docId, null);
    }

    /// <summary>Suffisso del callsign dopo l'ultimo '_' (es. LIRN_US0_APP → APP).</summary>
    private static string SuffixOf(string callsign) =>
        callsign.Contains('_') ? callsign[(callsign.LastIndexOf('_') + 1)..] : callsign;

    /// <summary>Deriva il tipo di settore dal suffisso del callsign (es. LIRF_TWR → Twr).</summary>
    private static SectorType ClassifySectorType(string callsign)
    {
        return SuffixOf(callsign).ToUpperInvariant() switch
        {
            "DEL" => SectorType.Del,
            "GND" => SectorType.Gnd,
            "TWR" => SectorType.Twr,
            "APP" or "DEP" => SectorType.App,
            _ => SectorType.Ctr,
        };
    }

    public async Task<int> AddSectorAsync(string firCode, string callsign, SectorType type, SectorKind kind, string name,
        string? defaultFrequency, int coverageOrder, ApproachKind? approachKind, int? parentSectorId,
        int? airportId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        callsign = (callsign ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (callsign.Length == 0) throw new ValidationException("Callsign obbligatorio (es. LIRR_NE_CTR).");
        if (name.Length == 0) throw new ValidationException("Nome settore obbligatorio.");
        if (kind == SectorKind.Airport && airportId is null) throw new ValidationException("Seleziona l'aeroporto del settore.");
        // I settori d'aeroporto (DEL/GND/TWR/APP) provengono dalla sorgente quando «Settori» è importato:
        // in tal caso si generano da «Genera documenti», non si aggiungono a mano. I settori d'area (ACC) restano liberi.
        if (kind == SectorKind.Airport && (await _policy.GetAsync(ct)).Sectors)
            throw new ValidationException("I settori d'aeroporto sono gestiti dalla sorgente (sola lettura): usa «Genera documenti». Per aggiungerli a mano, escludi «Settori» in «Sorgenti dati».");
        if (await _repo.CallsignExistsAsync(callsign, ct)) throw new ValidationException($"Callsign {callsign} già esistente.");
        return await _repo.AddSectorAsync(firCode, callsign, type, kind, name,
            string.IsNullOrWhiteSpace(defaultFrequency) ? null : defaultFrequency.Trim(),
            coverageOrder, type == SectorType.App ? approachKind : null, parentSectorId,
            kind == SectorKind.Airport ? airportId : null, ct);
    }

    public async Task DeleteSectorAsync(string firCode, int sectorId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.DeleteSectorAsync(firCode, sectorId, ct);
    }

    public async Task SetFeaturedAirportsAsync(string firCode, IReadOnlyList<int> orderedAirportIds, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.SetFeaturedAirportsAsync(firCode, orderedAirportIds ?? Array.Empty<int>(), ct);
    }

    public async Task SetFeaturedAppsAsync(string firCode, IReadOnlyList<int> orderedAppSectorIds, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.SetFeaturedAppsAsync(firCode, orderedAppSectorIds ?? Array.Empty<int>(), ct);
    }

    public async Task SetFeaturedVloasAsync(string firCode, IReadOnlyList<int> orderedVloaDocIds, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.SetFeaturedVloasAsync(firCode, orderedVloaDocIds ?? Array.Empty<int>(), ct);
    }

    public async Task<int> AddFrequencyAsync(string firCode, int sectorId, string label, string callsign,
        string frequencyMhz, bool isPrimary, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        label = (label ?? "").Trim();
        callsign = (callsign ?? "").Trim().ToUpperInvariant();
        frequencyMhz = (frequencyMhz ?? "").Trim();
        if (label.Length == 0) throw new ValidationException("Etichetta frequenza obbligatoria.");
        if (frequencyMhz.Length == 0) throw new ValidationException("Frequenza obbligatoria (es. 118.700).");
        return await _repo.AddFrequencyAsync(firCode, sectorId, label, callsign, frequencyMhz, isPrimary, ct);
    }

    public async Task DeleteFrequencyAsync(string firCode, int frequencyId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.DeleteFrequencyAsync(firCode, frequencyId, ct);
    }
}
