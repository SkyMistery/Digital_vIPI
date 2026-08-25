using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case di authoring dell'anagrafica/struttura ACC. La creazione di una ACC è admin-only (non esiste
/// ancora una ACC su cui dare grant); le altre scritture sono ACC-gated via <see cref="IEditAuthorizationService"/>.
/// Validazioni hard sugli input (univocità callsign/sectorKey, campi obbligatori). Riusa <see cref="ValidationException"/>.
/// </summary>
public interface IStructureEditingService
{
    Task<IReadOnlyList<AccRow>> ListAccsAsync(CancellationToken ct = default);
    Task<StructureData?> LoadAsync(string accCode, CancellationToken ct = default);

    Task<int> CreateAccAsync(string code, string name, string? countryPrefix, CancellationToken ct = default);
    Task DeleteAccAsync(string accCode, CancellationToken ct = default);

    Task<int> CreateAirportAsync(string accCode, string icao, string name, CancellationToken ct = default);
    Task DeleteAirportAsync(string accCode, int airportId, CancellationToken ct = default);
    Task MoveAirportAsync(int airportId, string fromAccCode, string targetAccCode, CancellationToken ct = default);

    /// <summary>Gestione aeroporti (admin): elenco cross-ACC + settori per i menu.</summary>
    Task<IReadOnlyList<AirportAdminRow>> ListAllAirportsAsync(CancellationToken ct = default);

    /// <summary>Cerca un aeroporto per ICAO sulla sorgente esterna (anche estero), SOLO per leggerne il nome
    /// (es. aeroporto fuori DB nei trasferimenti). NON lo inserisce nel catalogo. null = non trovato.</summary>
    Task<ExternalAirportInfo?> LookupExternalAirportAsync(string icao, CancellationToken ct = default);

    /// <summary>Nasconde/mostra un aeroporto (ACC-gated): la pagina pubblica e gli elenchi non lo mostrano più.</summary>
    Task SetAirportHiddenAsync(string accCode, int airportId, bool hidden, CancellationToken ct = default);

    /// <summary>Segna/desegna un aeroporto come «solo militare» (nessun traffico civile). Vedi la porta omonima.</summary>
    Task SetAirportMilitaryOnlyAsync(string accCode, int airportId, bool militaryOnly, CancellationToken ct = default);
    Task<IReadOnlyList<SectorBriefRow>> ListAllSectorsAsync(CancellationToken ct = default);

    /// <summary>Vista globale (cross-ACC) dei settori attivi col prefisso nazione e l'albero, per il picker di «Nuovo documento».</summary>
    Task<IReadOnlyList<GlobalSectorRow>> ListSectorNodesAsync(CancellationToken ct = default);

    /// <summary>
    /// Assegna automaticamente (admin) gli aeroporti dell'anagrafica IVAO la cui ACC di competenza
    /// (centerId) esiste già nel DB e che non sono ancora assegnati. Ritorna gli assegnati + gli
    /// aeroporti il cui import settori è fallito (da loggare).
    /// </summary>
    Task<AirportImportResult> AutoAssignKnownAirportsAsync(CancellationToken ct = default);

    /// <summary>
    /// Genera (admin) il documento di aeroporto per un ICAO già assegnato: scarica postazioni ATC + piste da IVAO,
    /// crea i settori DEL/GND/TWR (APP ignorato per ora) e il documento vIPI pubblicato. Idempotente.
    /// </summary>
    Task<AirportDocResult> GenerateAirportDocumentAsync(string icao, CancellationToken ct = default);

    Task<int> AddSectorAsync(string accCode, string callsign, SectorType type, SectorKind kind, string name,
        string? defaultFrequency, int coverageOrder, ApproachKind? approachKind, int? parentSectorId,
        int? airportId, CancellationToken ct = default);
    Task DeleteSectorAsync(string accCode, int sectorId, CancellationToken ct = default);

    /// <summary>Imposta gli aeroporti "in evidenza" della ACC (ordine = FeaturedRank 1..3) per la landing ACC.</summary>
    Task SetFeaturedAirportsAsync(string accCode, IReadOnlyList<int> orderedAirportIds, CancellationToken ct = default);
    /// <summary>Imposta gli APP "in evidenza" della ACC (ordine = FeaturedRank 1..3) per la landing ACC.</summary>
    Task SetFeaturedAppsAsync(string accCode, IReadOnlyList<int> orderedAppSectorIds, CancellationToken ct = default);
    /// <summary>Imposta le vLOA "in evidenza" della ACC (ordine = FeaturedRank 1..3) per la landing ACC.</summary>
    Task SetFeaturedVloasAsync(string accCode, IReadOnlyList<int> orderedVloaDocIds, CancellationToken ct = default);

    /// <summary>Imposta la frequenza (Sector.DefaultFrequency) di un settore della ACC. Solo settori seed/manuali:
    /// sui proiettati la frequenza è di sorgente (sola lettura), l'edit viene rifiutato con <see cref="ValidationException"/>.</summary>
    Task SetSectorFrequencyAsync(string accCode, int sectorId, string? frequencyMhz, CancellationToken ct = default);
}

/// <inheritdoc cref="IStructureEditingService"/>
public sealed class StructureEditingService : IStructureEditingService
{
    private readonly IStructureEditingRepository _repo;
    private readonly IAirportRepository _profile;
    private readonly IEditAuthorizationService _authz;
    private readonly IAirportDirectory _directory;
    private readonly IAirportDetailProvider _details;
    private readonly IImportPolicyStore _policy;
    private readonly IAirportSectorRepository _airportSectors;
    private readonly IAirportSectorImporter _sectorImporter;
    private readonly ISectorProjectionService _projection;

    private readonly IAirportImportUseCase _airportImport;

    /// <summary>Dice a TUTTE le sessioni che la mappa degli aeroporti è cambiata: la loro cache è scoped al
    /// circuito e senza una spinta invecchierebbe per ore. Vedi <see cref="SetAirportMilitaryOnlyAsync"/>.</summary>
    private readonly IStationCatalogVersion _catalog;

    public StructureEditingService(
        IStructureEditingRepository repo, IAirportRepository profile, IEditAuthorizationService authz,
        IAirportDirectory directory, IAirportDetailProvider details, IImportPolicyStore policy,
        IAirportSectorRepository airportSectors, IAirportSectorImporter sectorImporter,
        ISectorProjectionService projection, IAirportImportUseCase airportImport,
        IStationCatalogVersion catalog)
    {
        _catalog = catalog;
        _repo = repo;
        _profile = profile;
        _authz = authz;
        _directory = directory;
        _details = details;
        _policy = policy;
        _airportSectors = airportSectors;
        _sectorImporter = sectorImporter;
        _projection = projection;
        _airportImport = airportImport;
    }

    public Task<IReadOnlyList<AccRow>> ListAccsAsync(CancellationToken ct = default) => _repo.ListAccsAsync(ct);

    public Task<StructureData?> LoadAsync(string accCode, CancellationToken ct = default) => _repo.LoadAsync(accCode, ct);

    public async Task<int> CreateAccAsync(string code, string name, string? countryPrefix, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        code = (code ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (code.Length is < 2 or > 8) throw new ValidationException("Codice ACC non valido (es. LIRR).");
        if (name.Length == 0) throw new ValidationException("Nome ACC obbligatorio.");
        if (await _repo.AccExistsAsync(code, ct)) throw new ValidationException($"ACC {code} già esistente.");
        var prefix = string.IsNullOrWhiteSpace(countryPrefix) ? code.Substring(0, 2) : countryPrefix.Trim().ToUpperInvariant();
        return await _repo.CreateAccAsync(code, name, prefix, ct);
    }

    public async Task DeleteAccAsync(string accCode, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.DeleteAccAsync(accCode, ct);
    }

    public async Task<int> CreateAirportAsync(string accCode, string icao, string name, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        icao = (icao ?? "").Trim().ToUpperInvariant();
        name = (name ?? "").Trim();
        if (icao.Length != 4) throw new ValidationException("ICAO aeroporto non valido (4 lettere, es. LIRF).");
        if (name.Length == 0) throw new ValidationException("Nome aeroporto obbligatorio.");
        if (await _repo.AirportIcaoExistsAsync(icao, ct)) throw new ValidationException($"Aeroporto {icao} già esistente.");
        return await _repo.CreateAirportAsync(accCode, icao, name, ct);
    }

    public async Task DeleteAirportAsync(string accCode, int airportId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeleteAirportAsync(accCode, airportId, ct);
    }

    public async Task MoveAirportAsync(int airportId, string fromAccCode, string targetAccCode, CancellationToken ct = default)
    {
        // Spostamento cross-ACC: serve poter editare sia origine sia destinazione.
        await _authz.EnsureCanEditAccAsync(fromAccCode, ct);
        await _authz.EnsureCanEditAccAsync(targetAccCode, ct);
        await _repo.MoveAirportAsync(airportId, targetAccCode, ct);
    }

    public Task<IReadOnlyList<AirportAdminRow>> ListAllAirportsAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _repo.ListAllAirportsAsync(ct);
    }

    public async Task<ExternalAirportInfo?> LookupExternalAirportAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        if (icao.Length < 3) return null;   // ICAO plausibile
        var a = await _directory.GetByIcaoAsync(icao, ct);
        return a is null ? null : new ExternalAirportInfo(a.Icao, a.Name, a.City, a.AccCode);
    }

    public async Task SetAirportHiddenAsync(string accCode, int airportId, bool hidden, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.SetAirportHiddenAsync(accCode, airportId, hidden, ct);
    }

    public async Task SetAirportMilitaryOnlyAsync(string accCode, int airportId, bool militaryOnly, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.SetAirportMilitaryOnlyAsync(accCode, airportId, militaryOnly, ct);
        // ⚠️ Senza questo il segno resterebbe vecchio per ORE. La mappa degli aeroporti sta in una cache
        // SCOPED, e in Blazor Server lo scope è il circuito, cioè l'intera sessione: chi ha la pagina aperta
        // continuerebbe a vedere l'etichetta di prima, senza modo di capire perché.
        _catalog.Bump();
    }

    public Task<IReadOnlyList<SectorBriefRow>> ListAllSectorsAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _repo.ListAllSectorsAsync(ct);
    }

    public Task<IReadOnlyList<GlobalSectorRow>> ListSectorNodesAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _repo.ListSectorNodesAsync(ct);
    }

    public async Task<AirportImportResult> AutoAssignKnownAirportsAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();                       // solo il chiamante manual applica il guard
        return await _airportImport.RunAsync(ct);   // core anagrafica (doc 03 §4.2); i Failures li logga la UI
    }

    public async Task<AirportDocResult> GenerateAirportDocumentAsync(string icao, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return await GenerateAirportDocumentCoreAsync(icao, ct);
    }

    private async Task<AirportDocResult> GenerateAirportDocumentCoreAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        if (icao.Length != 4) throw new ValidationException("ICAO aeroporto non valido.");

        // Una lettura sola della policy per tutta la generazione: decide sia il catalogo settori sia cosa
        // finisce nel merge (TA e piste).
        var policy = await _policy.GetAsync(ct);

        // Fonte unica: il catalogo AirportSector. Se vuoto, lo importo prima (così funziona anche il bottone admin).
        var catalog = await _airportSectors.ListByAirportAsync(icao, ct);
        if (catalog.Count == 0)
        {
            await _sectorImporter.ImportAsync(icao, ct);
            catalog = await _airportSectors.ListByAirportAsync(icao, ct);

            // ⚠️ Con «Settori» escluso in Sorgenti l'import non fa nulla per scelta: il catalogo resta vuoto e
            // il documento uscirebbe senza settori e senza spiegazioni. Lo si dice, invece di generarlo monco.
            if (catalog.Count == 0 && !policy.IsImported(ImportCategory.Sectors))
                return new AirportDocResult(icao, false, 0, null,
                    "Settori esclusi in «Sorgenti dati»: aggiungi i settori d'aeroporto a mano in Struttura.");
        }

        // Postazioni d'aeroporto (non nascoste) → settori operativi (DEL/GND/TWR/APP), un settore per tipo.
        var sectors = catalog
            .Where(s => !s.IsHidden)
            .Select(s => (Type: ClassifySectorType(s.Position, s.ComposePosition), s.ComposePosition, s.Frequency))
            .Where(x => x.Type is SectorType.Del or SectorType.Gnd or SectorType.Twr or SectorType.App)
            .GroupBy(x => x.Type).Select(g => g.First())
            .Select(x => (x.Type, Callsign: x.ComposePosition, x.Frequency))
            .ToList();

        // 1 — assicura i settori d'aeroporto (DEL/GND/TWR/APP + fallback TWR).
        var (created, found) = await _repo.EnsureAirportSectorsAsync(icao, sectors, ct);
        if (!found) return new AirportDocResult(icao, false, 0, null, "Aeroporto non assegnato a una ACC.");

        // Piste (dalla sorgente dettaglio, non dal catalogo) e Transition Altitude (dall'anagrafica), ma solo
        // per le categorie che la policy dichiara di sorgente: la decisione sta in un punto solo, condiviso
        // col reimport dell'editor aeroporto. ⚠️ Senza questo, generare il documento sovrascriveva la TA e le
        // piste scritte a mano anche con le categorie escluse in «Sorgenti dati» (vedi SourceMergeInputs).
        var (ta, runways) = await SourceMergeInputs.ReadAsync(policy, icao, _directory, _details, ct);

        // 2 — merge nel profilo strutturato (preserva l'editoriale) e 3 — rigenera il documento (Frequenze dal catalogo).
        await _profile.MergeFromSourceAsync(icao, ta, runways, ct);
        var docId = await _profile.RebuildDocumentAsync(icao, ct);
        return new AirportDocResult(icao, true, created, docId, null);
    }

    /// <summary>Deriva il tipo di settore dalla position (fallback dal suffisso del callsign, es. LIRN_US0_APP → APP).</summary>
    private static SectorType ClassifySectorType(string? position, string callsign)
    {
        var p = (position ?? "").Trim().ToUpperInvariant();
        if (p.Length == 0)
            p = callsign.Contains('_') ? callsign[(callsign.LastIndexOf('_') + 1)..].ToUpperInvariant() : callsign.ToUpperInvariant();
        return p switch
        {
            "DEL" => SectorType.Del,
            "GND" => SectorType.Gnd,
            "TWR" => SectorType.Twr,
            "APP" or "DEP" => SectorType.App,
            _ => SectorType.Ctr,
        };
    }

    public async Task<int> AddSectorAsync(string accCode, string callsign, SectorType type, SectorKind kind, string name,
        string? defaultFrequency, int coverageOrder, ApproachKind? approachKind, int? parentSectorId,
        int? airportId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
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
        return await _repo.AddSectorAsync(accCode, callsign, type, kind, name,
            string.IsNullOrWhiteSpace(defaultFrequency) ? null : defaultFrequency.Trim(),
            coverageOrder, type == SectorType.App ? approachKind : null, parentSectorId,
            kind == SectorKind.Airport ? airportId : null, ct);
    }

    public async Task DeleteSectorAsync(string accCode, int sectorId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeleteSectorAsync(accCode, sectorId, ct);
    }

    public async Task SetFeaturedAirportsAsync(string accCode, IReadOnlyList<int> orderedAirportIds, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.SetFeaturedAirportsAsync(accCode, orderedAirportIds ?? Array.Empty<int>(), ct);
    }

    public async Task SetFeaturedAppsAsync(string accCode, IReadOnlyList<int> orderedAppSectorIds, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.SetFeaturedAppsAsync(accCode, orderedAppSectorIds ?? Array.Empty<int>(), ct);
    }

    public async Task SetFeaturedVloasAsync(string accCode, IReadOnlyList<int> orderedVloaDocIds, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.SetFeaturedVloasAsync(accCode, orderedVloaDocIds ?? Array.Empty<int>(), ct);
    }

    public async Task SetSectorFrequencyAsync(string accCode, int sectorId, string? frequencyMhz, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.SetSectorFrequencyAsync(accCode, sectorId, (frequencyMhz ?? "").Trim(), ct);
    }
}
