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
public interface INeighbourImportService
{
    Task<NeighbourImportResult> ImportAndComputeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NeighbourCandidateRow>> ListAsync(CancellationToken ct = default);

    /// <summary>Ricalcola on-demand il dettaglio di adiacenza di una coppia (settori adiacenti + shapes per mappa),
    /// per far verificare all'admin se il confine è reale. Non persiste nulla.</summary>
    Task<NeighbourPairDetail> GetPairDetailAsync(int id, CancellationToken ct = default);
    Task SetStatusAsync(int id, NeighbourCandidateStatus status, CancellationToken ct = default);
    Task SetPolygonAsync(int id, string? regionMapPolygon, CancellationToken ct = default);
    Task<int> AddManualAsync(string homeAccCode, string foreignAccCode, string foreignAccName,
        string countryId, string foreignRootCallsign, string? regionMapPolygon, CancellationToken ct = default);
    Task<int> GenerateVloaAsync(int id, CancellationToken ct = default);
}

/// <inheritdoc cref="INeighbourImportService"/>
public sealed class NeighbourImportService : INeighbourImportService
{
    private readonly INeighbourRepository _repo;
    private readonly IAccDirectory _directory;
    private readonly IEditAuthorizationService _authz;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NeighboursOptions _opt;

    public NeighbourImportService(INeighbourRepository repo, IAccDirectory directory,
        IEditAuthorizationService authz, IServiceScopeFactory scopeFactory, IOptions<NeighboursOptions> opt)
    {
        _repo = repo;
        _directory = directory;
        _authz = authz;
        _scopeFactory = scopeFactory;
        _opt = opt.Value;
    }

    public async Task<NeighbourImportResult> ImportAndComputeAsync(CancellationToken ct = default)
    {
        try { return await ImportAndComputeCoreAsync(ct); }
        catch (Exception ex) { NeighbourDebugLog.Log($"IMPORT EX: {ex}"); throw; }
    }

    private async Task<NeighbourImportResult> ImportAndComputeCoreAsync(CancellationToken ct)
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

        // 1) Poligoni domestici (una volta), pre-parsati in Ring.
        var domestic = await repo.ListDomesticSectorPolygonsAsync(ct);
        NeighbourDebugLog.Log($"Domestic sectors with polygon: {domestic.Count}");
        var domesticRings = domestic
            .Where(d => IsAccBoundaryPosition(d.ComposePosition))
            .Select(d => (d.CenterId, d.ComposePosition, Ring: PolygonGeometry.ToRing(d.RegionMapPolygon)))
            .Where(x => x.Ring is not null)
            .ToList();
        if (domesticRings.Count == 0)
            warnings.Add("Nessun settore domestico con poligono: importa prima gli ACC italiani (pagina ACC).");

        var domesticCodes = new HashSet<string>(await repo.ListDomesticAccCodesAsync(ct), StringComparer.OrdinalIgnoreCase);
        NeighbourDebugLog.Log($"Domestic ACC codes: {domesticCodes.Count} · rings: {domesticRings.Count} · countries cfg: {_opt.CountryIds.Count} [{string.Join(",", _opt.CountryIds)}] · threshold {threshold}");

        var warnBag = new System.Collections.Concurrent.ConcurrentBag<string>();
        foreach (var w in warnings) warnBag.Add(w);

        // 2a) Elenco ACC esteri: un GET /centers per paese (in parallelo). Cheap.
        var countryList = _opt.CountryIds.Select(c => c.Trim())
            .Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var foreignAccList = new System.Collections.Concurrent.ConcurrentBag<(string Country, string Code, string Name)>();
        await RunBoundedAsync(countryList, 6, async country =>
        {
            IReadOnlyList<Abstractions.SourceCenter> centers;
            try { centers = await _directory.GetCentersByCountryAsync(country, ct); }
            catch (Exception ex) { warnBag.Add($"{country}: import ACC fallito ({ex.Message})."); return; }

            foreach (var fCode in centers.Select(c => c.CenterId)
                         .Where(c => !string.IsNullOrWhiteSpace(c) && !domesticCodes.Contains(c))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fName = centers.FirstOrDefault(c => string.Equals(c.CenterId, fCode, StringComparison.OrdinalIgnoreCase))?.Name ?? fCode;
                foreignAccList.Add((country, fCode.ToUpperInvariant(), fName));
            }
        });
        NeighbourDebugLog.Log($"Foreign ACCs listed: {foreignAccList.Count} [{string.Join(",", foreignAccList.Select(f => f.Code).Distinct())}]");

        // 2b) Per ogni ACC estero (in parallelo, throttled): subcenter + adiacenza. Il costo dominante sono
        // le GET di dettaglio per subcenter → parallelizzare gli ACC riduce di molto il tempo totale.
        var hits = new System.Collections.Concurrent.ConcurrentBag<(string Home, string HomeSect, string Foreign, string ForeignSect, string Name, string Country, double Dist, string? Poly)>();
        // Subcenter esteri risultati CONFINANTI (≥1 adiacenza): da persistire come catalogo AccSector.
        var confiningSubs = new System.Collections.Concurrent.ConcurrentDictionary<string,
            (string Name, System.Collections.Concurrent.ConcurrentDictionary<string, Abstractions.SourceSubcenter> Subs)>(StringComparer.OrdinalIgnoreCase);
        await RunBoundedAsync(foreignAccList.ToList(), 6, async fa =>
        {
            IReadOnlyList<Abstractions.SourceSubcenter> subs;
            try { subs = await _directory.GetSubcentersAsync(fa.Code, ct); }
            catch (Exception ex) { warnBag.Add($"{fa.Code}: subcenter non letti ({ex.Message})."); return; }

            foreach (var sub in subs)
            {
                if (!IsAccBoundaryPosition(sub.ComposePosition)) continue;   // TWR/APP/GND ecc. non sono confini ACC
                var fRing = PolygonGeometry.ToRing(sub.RegionMapPolygon);
                if (fRing is null) continue;   // senza poligono → non calcolabile qui (fallback manuale in UI)
                var fSpanLat = fRing.MaxLat - fRing.MinLat;
                var fSpanLon = fRing.MaxLon - fRing.MinLon;

                var subHit = false;
                foreach (var (homeCode, homeSect, homeRing) in domesticRings)
                {
                    if (!PolygonGeometry.AreAdjacent(homeRing, fRing, threshold)) continue;
                    var dist = PolygonGeometry.MinEdgeDistanceNm(homeRing!.Points, fRing.Points);
                    hits.Add((homeCode.ToUpperInvariant(), homeSect, fa.Code, sub.ComposePosition, fa.Name, fa.Country, dist, sub.RegionMapPolygon));
                    subHit = true;
                    NeighbourDebugLog.Log($"  HIT {homeSect} × {sub.ComposePosition}  dist={dist:0.0}NM  fBbox=[{fRing.MinLat:0.0},{fRing.MinLon:0.0}..{fRing.MaxLat:0.0},{fRing.MaxLon:0.0}] span={fSpanLat:0.0}x{fSpanLon:0.0} pts={fRing.Points.Count}");
                }
                if (subHit)
                {
                    var entry = confiningSubs.GetOrAdd(fa.Code, _ =>
                        (fa.Name, new System.Collections.Concurrent.ConcurrentDictionary<string, Abstractions.SourceSubcenter>(StringComparer.OrdinalIgnoreCase)));
                    entry.Subs.TryAdd(sub.ComposePosition, sub);
                }
            }
        });

        // 2b-bis) Persisti il catalogo degli ACC esteri confinanti (Acc IsForeign + subcenter) e riproietta i Sector.
        // Necessario perché AoR/frequenze/gerarchie/coordinamenti della vLOA leggano dati veri (non solo la radice).
        var foreignCatalog = confiningSubs
            .Select(kv => new Abstractions.ForeignAccImport(kv.Key, kv.Value.Name, kv.Value.Subs.Values.ToList()))
            .ToList();
        if (foreignCatalog.Count > 0)
        {
            await repo.PersistForeignCatalogAsync(foreignCatalog, ct);
            var projection = dbScope.ServiceProvider.GetRequiredService<ISectorProjectionService>();
            await projection.SyncFromCatalogsAsync(ct);
            NeighbourDebugLog.Log($"Foreign catalog persisted: {foreignCatalog.Count} ACC, {foreignCatalog.Sum(f => f.Subcenters.Count)} subcenters. Projected.");
        }

        // 2c) Aggregazione per coppia (Home, Foreign): min distanza + conteggio settori adiacenti.
        var agg = new Dictionary<(string Home, string Foreign), NeighbourPairAggregate>();
        foreach (var h in hits)
        {
            var key = (h.Home, h.Foreign);
            if (!agg.TryGetValue(key, out var a))
                agg[key] = a = new NeighbourPairAggregate { ForeignName = h.Name, CountryId = h.Country };
            a.Count++;
            a.HomeSectors.Add(h.HomeSect);
            a.ForeignSectors.Add(h.ForeignSect);
            if (h.Dist < a.MinDist) { a.MinDist = h.Dist; a.BestForeignPolygon = h.Poly; }
        }

        foreach (var kv in agg.OrderByDescending(x => x.Value.Count))
            NeighbourDebugLog.Log($"PAIR {kv.Key.Home}↔{kv.Key.Foreign}  homeSect={kv.Value.HomeSectors.Count} foreignSect={kv.Value.ForeignSectors.Count} pairs={kv.Value.Count} minDist={kv.Value.MinDist:0.0}NM  fSect=[{string.Join(",", kv.Value.ForeignSectors)}]");

        var countries = countryList.Count;
        var foreignAccs = foreignAccList.Count;
        warnings = warnBag.ToList();

        // 3) Upsert dei candidati aggregati per coppia di ACC.
        var items = agg.Select(kv => new NeighbourCandidateUpsert(
            HomeAccCode: kv.Key.Home,
            ForeignAccCode: kv.Key.Foreign,
            ForeignAccName: kv.Value.ForeignName,
            CountryId: kv.Value.CountryId,
            ForeignRootCallsign: $"{kv.Key.Foreign}_CTR",
            RegionMapPolygon: kv.Value.BestForeignPolygon,
            MinDistanceNm: kv.Value.MinDist == double.MaxValue ? null : Math.Round(kv.Value.MinDist, 1),
            // Settori esteri distinti adiacenti — NON il numero di coppie settore×settore (che gonfia il conteggio).
            AdjacentSectorCount: kv.Value.ForeignSectors.Count,
            AdjacentHomeCallsigns: kv.Value.HomeSectors.ToList(),
            AdjacentForeignCallsigns: kv.Value.ForeignSectors.ToList())).ToList();

        NeighbourDebugLog.Log($"Hits: {hits.Count} · aggregated pairs: {items.Count} · warnings: {warnings.Count}");
        var (created, updated) = await repo.UpsertCandidatesAsync(items, ct);
        NeighbourDebugLog.Log($"Upsert done: created {created}, updated {updated}. Import end.");
        return new NeighbourImportResult(countries, foreignAccs, created, updated, warnings);
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
        var threshold = _opt.AdjacencyThresholdNm;
        var warnings = new List<string>();

        // Settori domestici (solo di questo ACC, solo confini CTR/FSS) col loro grezzo + Ring.
        var domestic = (await repo.ListDomesticSectorPolygonsAsync(ct))
            .Where(d => string.Equals(d.CenterId, home, StringComparison.OrdinalIgnoreCase)
                        && IsAccBoundaryPosition(d.ComposePosition))
            .Select(d => (Sect: d.ComposePosition, Raw: d.RegionMapPolygon, Ring: PolygonGeometry.ToRing(d.RegionMapPolygon)))
            .Where(x => x.Ring is not null)
            .ToList();

        // Subcenter esteri (ri-scaricati da IVAO), stesso filtro.
        IReadOnlyList<Abstractions.SourceSubcenter> subs = Array.Empty<Abstractions.SourceSubcenter>();
        try { subs = await _directory.GetSubcentersAsync(foreign, ct); }
        catch (Exception ex) { warnings.Add($"{foreign}: subcenter non letti da IVAO ({ex.Message})."); }
        var foreignSects = subs
            .Where(s => IsAccBoundaryPosition(s.ComposePosition))
            .Select(s => (Sect: s.ComposePosition, Raw: s.RegionMapPolygon, Ring: PolygonGeometry.ToRing(s.RegionMapPolygon)))
            .Where(x => x.Ring is not null)
            .ToList();

        // Adiacenze settore↔settore.
        var adj = new List<NeighbourAdjacency>();
        var usedHome = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedForeign = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in foreignSects)
            foreach (var h in domestic)
            {
                if (!PolygonGeometry.AreAdjacent(h.Ring, f.Ring, threshold)) continue;
                var dist = PolygonGeometry.MinEdgeDistanceNm(h.Ring!.Points, f.Ring!.Points);
                adj.Add(new NeighbourAdjacency(h.Sect, f.Sect, Math.Round(dist, 1)));
                usedHome.Add(h.Sect);
                usedForeign.Add(f.Sect);
            }
        adj = adj.OrderBy(a => a.DistanceNm).ThenBy(a => a.HomeSector).ToList();

        // Mappa: disegna i settori coinvolti nell'adiacenza; se nessuno confina, mostra tutti (per far vedere il perché).
        var homeShown = domestic.Where(d => usedHome.Contains(d.Sect)).ToList();
        if (homeShown.Count == 0) homeShown = domestic;
        var foreignShown = foreignSects.Where(f => usedForeign.Contains(f.Sect)).ToList();
        if (foreignShown.Count == 0) foreignShown = foreignSects;

        // Proiezione condivisa: prima i domestici, poi gli esteri (ordine preservato per indice).
        var raws = homeShown.Select(h => (string?)h.Raw).Concat(foreignShown.Select(f => (string?)f.Raw)).ToList();
        var proj = AorPolygonProjector.ProjectShared(raws);

        var homeShapes = new List<NeighbourMapShape>();
        var foreignShapes = new List<NeighbourMapShape>();
        if (proj is not null)
        {
            for (var i = 0; i < homeShown.Count; i++)
                if (proj.Polygons[i] is { } p) homeShapes.Add(new NeighbourMapShape(homeShown[i].Sect, p.Path, LatLng(homeShown[i].Raw)));
            for (var i = 0; i < foreignShown.Count; i++)
                if (proj.Polygons[homeShown.Count + i] is { } p) foreignShapes.Add(new NeighbourMapShape(foreignShown[i].Sect, p.Path, LatLng(foreignShown[i].Raw)));
        }
        if (foreignSects.Count == 0 && warnings.Count == 0)
            warnings.Add($"{foreign}: nessun settore CTR/FSS con poligono da IVAO (usa il fallback poligono manuale).");

        return new NeighbourPairDetail(home, foreign, adj, proj?.ViewBox, homeShapes, foreignShapes, warnings);
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

    /// <summary>
    /// Solo le posizioni center (CTR) e flight service (FSS) definiscono i confini di un ACC. Le posizioni locali
    /// all'aeroporto (TWR, APP incl. <c>I_TWR</c>/<c>G_APP</c>, GND, DEL, ATIS…) NON sono confini ACC reali e vanno
    /// escluse dal calcolo di adiacenza. Filtro sul suffisso del callsign (dopo l'ultimo <c>_</c>).
    /// </summary>
    /// <summary>Punti [lat,lng] dell'anello grezzo, per la mappa Leaflet reale (lista vuota se non parsabile).</summary>
    private static IReadOnlyList<double[]> LatLng(string? raw) =>
        PolygonGeometry.ParsePoints(raw).Select(p => new[] { p.Lat, p.Lon }).ToList();

    private static bool IsAccBoundaryPosition(string? composePosition)
    {
        if (string.IsNullOrWhiteSpace(composePosition)) return false;
        var i = composePosition.LastIndexOf('_');
        var suffix = (i >= 0 ? composePosition[(i + 1)..] : composePosition).Trim();
        return suffix.Equals("CTR", StringComparison.OrdinalIgnoreCase)
            || suffix.Equals("FSS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Esegue <paramref name="body"/> su tutti gli item con al massimo <paramref name="dop"/> in volo.</summary>
    private static async Task RunBoundedAsync<T>(IReadOnlyList<T> items, int dop, Func<T, Task> body)
    {
        using var gate = new SemaphoreSlim(dop);
        var tasks = items.Select(async item =>
        {
            await gate.WaitAsync();
            try { await body(item); }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
    }
}
