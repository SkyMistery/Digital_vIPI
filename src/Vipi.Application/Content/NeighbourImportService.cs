using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case (admin-only) per generare le vLOA con gli ACC confinanti. Scarica da IVAO gli ACC/settori dei paesi
/// vicini (<c>Neighbours:CountryIds</c>), calcola quali settori confinano geometricamente con i settori domestici
/// (<see cref="PolygonGeometry.AreAdjacent"/>), aggrega per coppia di ACC e fa staging dei candidati. L'admin
/// conferma/rifiuta le coppie; alla conferma si materializzano ACC/settore esteri e si genera la vLOA. I dati
/// esteri NON confinanti non vengono persistiti.
/// </summary>
public interface INeighbourImportService : INeighbourReader
{
    /// <param name="progress">Facoltativo: avanzamento delle GET di dettaglio (la parte lunga).</param>
    Task<NeighbourImportResult> ImportAndComputeAsync(CancellationToken ct = default,
        IProgress<ForeignAccFetchProgress>? progress = null);

    /// <summary>Ricalcola on-demand il dettaglio di adiacenza di una coppia (settori adiacenti + shapes per mappa),
    /// per far verificare all'admin se il confine è reale. Non persiste nulla.</summary>
    Task<NeighbourPairDetail> GetPairDetailAsync(int id, CancellationToken ct = default);
    Task SetStatusAsync(int id, NeighbourCandidateStatus status, CancellationToken ct = default);
    Task SetPolygonAsync(int id, string? regionMapPolygon, CancellationToken ct = default);
    Task<int> AddManualAsync(string homeAccCode, string foreignAccCode, string foreignAccName,
        string countryId, string foreignRootCallsign, string? regionMapPolygon, CancellationToken ct = default);
    Task<int> GenerateVloaAsync(int id, CancellationToken ct = default);

    /// <summary>Aggiunge a mano un settore all'ACC estero di una coppia CONFERMATA: verifica il callsign sulla sorgente
    /// (IVAO), lo materializza come <c>AccSector</c> sotto l'ACC estero e riproietta. Errore se la coppia non è
    /// confermata, se il callsign non esiste sulla sorgente, o se appartiene già a un altro ACC (no hijack).</summary>
    Task<AddForeignSectorResult> AddForeignSectorAsync(int candidateId, string callsign, CancellationToken ct = default);
}

/// <summary>Esito dell'aggiunta manuale di un settore estero. <paramref name="AlreadyPresent"/>=true → il callsign era
/// già sotto lo stesso ACC (nessuna modifica; <paramref name="Hidden"/> indica se è nascosto).</summary>
public sealed record AddForeignSectorResult(string Callsign, string ForeignAccCode, bool AlreadyPresent, bool Hidden);

/// <inheritdoc cref="INeighbourImportService"/>
public sealed class NeighbourImportService : INeighbourImportService
{
    private readonly INeighbourRepository _repo;
    private readonly IAccDirectory _directory;
    private readonly IEditAuthorizationService _authz;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NeighboursOptions _opt;
    private readonly ForeignAccFetcher _fetcher;
    private readonly NeighbourAdjacencyComputer _computer;
    private readonly ForeignSectorResolver _sectorResolver;

    public NeighbourImportService(INeighbourRepository repo, IAccDirectory directory,
        IEditAuthorizationService authz, IServiceScopeFactory scopeFactory, IOptions<NeighboursOptions> opt,
        ForeignAccFetcher fetcher, NeighbourAdjacencyComputer computer, ForeignSectorResolver sectorResolver)
    {
        _repo = repo;
        _directory = directory;
        _authz = authz;
        _scopeFactory = scopeFactory;
        _opt = opt.Value;
        _fetcher = fetcher;
        _computer = computer;
        _sectorResolver = sectorResolver;
    }

    public async Task<NeighbourImportResult> ImportAndComputeAsync(CancellationToken ct = default,
        IProgress<ForeignAccFetchProgress>? progress = null)
    {
        try { return await ImportAndComputeCoreAsync(ct, progress); }
        // L'interruzione chiesta da chi guarda non è un guaio da registrare: risale pulita alla UI.
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { NeighbourDebugLog.Log($"IMPORT EX: {ex}"); throw; }
    }

    private async Task<NeighbourImportResult> ImportAndComputeCoreAsync(CancellationToken ct,
        IProgress<ForeignAccFetchProgress>? progress)
    {
        NeighbourDebugLog.Log("Import start");
        _authz.EnsureAdmin();
        NeighbourDebugLog.Log("Admin ok");

        // L'import dura ~30s (centinaia di GET IVAO). Il VipiDbContext iniettato è scoped al circuito Blazor
        // e può essere disposto se il circuito si ricicla durante l'attesa (→ ObjectDisposedException nell'upsert
        // finale). Usiamo uno scope DI dedicato, con un context indipendente dal ciclo di vita del circuito.
        using var dbScope = _scopeFactory.CreateScope();
        var repo = dbScope.ServiceProvider.GetRequiredService<INeighbourRepository>();

        var threshold = _opt.AdjacencyThresholdNm;
        var warnings = new List<string>();

        // 1) Poligoni domestici (una volta). Il calcolo di adiacenza è nel NeighbourAdjacencyComputer (puro).
        var domestic = await repo.ListDomesticSectorPolygonsAsync(ct);
        NeighbourDebugLog.Log($"Domestic sectors with polygon: {domestic.Count}");
        var hasDomesticRings = domestic.Any(d => NeighbourAdjacencyComputer.IsAccBoundaryPosition(d.ComposePosition)
            && PolygonGeometry.ToRing(d.RegionMapPolygon) is not null);
        if (!hasDomesticRings)
            warnings.Add("Nessun settore domestico con poligono: importa prima gli ACC italiani (pagina ACC).");

        var domesticCodes = new HashSet<string>(await repo.ListDomesticAccCodesAsync(ct), StringComparer.OrdinalIgnoreCase);
        NeighbourDebugLog.Log($"Domestic ACC codes: {domesticCodes.Count} · countries cfg: {_opt.CountryIds.Count} [{string.Join(",", _opt.CountryIds)}] · threshold {threshold}");

        // 2) Fetch ACC + subcenter esteri (IO, parallelo) → dati grezzi + warning.
        var (foreign, fetchWarnings) = await _fetcher.FetchAsync(_opt.CountryIds, domesticCodes, ct, progress);
        warnings.AddRange(fetchWarnings);
        NeighbourDebugLog.Log($"Foreign ACCs fetched: {foreign.Count} [{string.Join(",", foreign.Select(f => f.Code))}]");

        // 3) Calcolo adiacenza + aggregazione (puro, testato).
        var computed = _computer.ComputeImport(domestic, foreign, threshold);
        foreach (var hit in computed.Hits)
            NeighbourDebugLog.Log($"  HIT {hit.HomeSector} × {hit.ForeignSector}  dist={hit.DistanceNm:0.0}NM");

        // 3b) Persisti il catalogo degli ACC esteri confinanti (Acc IsForeign + subcenter) e riproietta i Sector.
        //     Atomico: persist + riproiezione vivono/muoiono insieme (un persist committato senza riproiezione
        //     lascerebbe ACC esteri senza settori operativi = stato incoerente). Stesso context scoped → stessa tx.
        if (computed.ForeignCatalog.Count > 0)
        {
            var uow = dbScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var projection = dbScope.ServiceProvider.GetRequiredService<ISectorProjectionService>();
            await uow.ExecuteInTransactionAsync(async c =>
            {
                await repo.PersistForeignCatalogAsync(computed.ForeignCatalog, c);
                await projection.SyncFromCatalogsAsync(c);
            }, ct);
            NeighbourDebugLog.Log($"Foreign catalog persisted: {computed.ForeignCatalog.Count} ACC, {computed.ForeignCatalog.Sum(f => f.Subcenters.Count)} subcenters. Projected.");
        }

        foreach (var c in computed.Candidates)
            NeighbourDebugLog.Log($"PAIR {c.HomeAccCode}↔{c.ForeignAccCode}  foreignSect={c.AdjacentSectorCount} minDist={c.MinDistanceNm:0.0}NM  fSect=[{string.Join(",", c.AdjacentForeignCallsigns ?? Array.Empty<string>())}]");

        // 4) Upsert dei candidati aggregati per coppia di ACC.
        NeighbourDebugLog.Log($"Hits: {computed.Hits.Count} · aggregated pairs: {computed.Candidates.Count} · warnings: {warnings.Count}");
        var (created, updated) = await repo.UpsertCandidatesAsync(computed.Candidates, ct);
        NeighbourDebugLog.Log($"Upsert done: created {created}, updated {updated}. Import end.");

        var countries = _opt.CountryIds.Select(c => c.Trim())
            .Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return new NeighbourImportResult(countries, foreign.Count, created, updated, warnings);
    }

    public async Task<IReadOnlyList<NeighbourCandidateRow>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Domain.Entities.NeighbourCandidate> rows;
        try { rows = await _repo.ListCandidatesAsync(ct); }
        catch (Exception ex) { NeighbourDebugLog.Log($"LIST EX: {ex}"); throw; }
        return rows.Select(c => new NeighbourCandidateRow(
            c.Id, c.HomeAccCode, c.ForeignAccCode, c.ForeignAccName, c.CountryId, c.ForeignRootCallsign,
            !string.IsNullOrWhiteSpace(c.RegionMapPolygon), c.MinDistanceNm, c.AdjacentSectorCount,
            c.Status, c.VloaDocumentId)).ToList();
    }

    public async Task<NeighbourPairDetail> GetPairDetailAsync(int id, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        using var dbScope = _scopeFactory.CreateScope();
        var repo = dbScope.ServiceProvider.GetRequiredService<INeighbourRepository>();

        var cand = await repo.GetAsync(id, ct)
            ?? throw new Aor.ValidationException("Candidato inesistente.");
        var home = cand.HomeAccCode.ToUpperInvariant();
        var foreign = cand.ForeignAccCode.ToUpperInvariant();

        var domestic = await repo.ListDomesticSectorPolygonsAsync(ct);

        // Subcenter esteri ri-scaricati da IVAO (single ACC); l'eventuale fallita fetch diventa un seed-warning.
        IReadOnlyList<Abstractions.SourceSubcenter> subs = Array.Empty<Abstractions.SourceSubcenter>();
        var seedWarnings = new List<string>();
        try { subs = await _directory.GetSubcentersAsync(foreign, ct); }
        catch (Exception ex) { seedWarnings.Add($"{foreign}: subcenter non letti da IVAO ({ex.Message})."); }

        // Adiacenze + forme mappa: calcolo puro (testato) nel computer.
        return _computer.ComputePairDetail(home, foreign, domestic, subs, _opt.AdjacencyThresholdNm, seedWarnings);
    }

    public async Task SetStatusAsync(int id, NeighbourCandidateStatus status, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.SetStatusAsync(id, status, ct);
    }

    public async Task SetPolygonAsync(int id, string? regionMapPolygon, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        // Valida il poligono incollato: null da Project = JSON non parsabile / degenere.
        if (!string.IsNullOrWhiteSpace(regionMapPolygon) && AorPolygonProjector.Project(regionMapPolygon) is null)
            throw new Aor.ValidationException("Poligono non valido: atteso JSON [[lng,lat],…] o [{lat,lng},…] con ≥3 punti.");
        await _repo.SetPolygonAsync(id, regionMapPolygon, ct);
    }

    public async Task<int> AddManualAsync(string homeAccCode, string foreignAccCode, string foreignAccName,
        string countryId, string foreignRootCallsign, string? regionMapPolygon, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        homeAccCode = (homeAccCode ?? "").Trim().ToUpperInvariant();
        foreignAccCode = (foreignAccCode ?? "").Trim().ToUpperInvariant();
        if (homeAccCode.Length == 0 || foreignAccCode.Length == 0)
            throw new Aor.ValidationException("Codici ACC Home e Foreign obbligatori.");
        if (string.Equals(homeAccCode, foreignAccCode, StringComparison.OrdinalIgnoreCase))
            throw new Aor.ValidationException("Home e Foreign non possono coincidere.");
        if (!string.IsNullOrWhiteSpace(regionMapPolygon) && AorPolygonProjector.Project(regionMapPolygon) is null)
            throw new Aor.ValidationException("Poligono non valido.");

        foreignRootCallsign = (foreignRootCallsign ?? "").Trim().ToUpperInvariant();
        if (foreignRootCallsign.Length == 0) foreignRootCallsign = $"{foreignAccCode}_CTR";

        var item = new NeighbourCandidateUpsert(homeAccCode, foreignAccCode,
            string.IsNullOrWhiteSpace(foreignAccName) ? foreignAccCode : foreignAccName.Trim(),
            (countryId ?? "").Trim().ToUpperInvariant(), foreignRootCallsign, regionMapPolygon, null, 0);
        return await _repo.AddManualAsync(item, ct);
    }

    public async Task<int> GenerateVloaAsync(int id, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        var cand = await _repo.GetAsync(id, ct)
            ?? throw new Aor.ValidationException("Candidato inesistente.");
        if (cand.Status != NeighbourCandidateStatus.Confirmed)
            throw new Aor.ValidationException("Conferma prima la coppia, poi genera la vLOA.");
        return await _repo.MaterializeAndCreateVloaAsync(id, ct);
    }

    public async Task<AddForeignSectorResult> AddForeignSectorAsync(int candidateId, string callsign, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        var parsed = ForeignSectorCallsign.Parse(callsign);   // valida la forma (throwa ValidationException)

        // Scope DI dedicato: le fetch sorgente possono durare e il context del circuito Blazor potrebbe riciclarsi.
        using var dbScope = _scopeFactory.CreateScope();
        var repo = dbScope.ServiceProvider.GetRequiredService<INeighbourRepository>();

        var cand = await repo.GetAsync(candidateId, ct)
            ?? throw new Aor.ValidationException("Candidato inesistente.");
        if (cand.Status != NeighbourCandidateStatus.Confirmed)
            throw new Aor.ValidationException("Conferma prima la coppia, poi aggiungi settori esteri.");

        var foreignAcc = cand.ForeignAccCode.ToUpperInvariant();

        // Guard: il callsign è già catalogato? Stesso ACC → idempotente (solo avviso); altro ACC → rifiuta (no hijack).
        if (await repo.FindSectorOwnerAsync(parsed.Callsign, ct) is { } owner)
        {
            if (string.Equals(owner.AccCode, foreignAcc, StringComparison.OrdinalIgnoreCase))
                return new AddForeignSectorResult(parsed.Callsign, foreignAcc, AlreadyPresent: true, Hidden: owner.IsHidden);
            throw new Aor.ValidationException(
                $"«{parsed.Callsign}» appartiene già all'ACC {owner.AccCode} — non spostabile da qui.");
        }

        // Verifica sulla sorgente (dispatch per natura del callsign). Assente → errore.
        var sub = await _sectorResolver.ResolveAsync(parsed, foreignAcc, ct)
            ?? throw new Aor.ValidationException($"Settore «{parsed.Callsign}» non trovato sulla sorgente (IVAO).");

        // Persist (riuso PersistForeignCatalog: upsert Acc estero + AccSector) + riproiezione, atomico.
        var uow = dbScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var projection = dbScope.ServiceProvider.GetRequiredService<ISectorProjectionService>();
        var import = new ForeignAccImport(foreignAcc, cand.ForeignAccName, new[] { sub });
        await uow.ExecuteInTransactionAsync(async c =>
        {
            await repo.PersistForeignCatalogAsync(new[] { import }, c);
            await projection.SyncFromCatalogsAsync(c);
        }, ct);

        return new AddForeignSectorResult(parsed.Callsign, foreignAcc, AlreadyPresent: false, Hidden: false);
    }
}
