using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IHierarchyEditingService"/>
public sealed class EfHierarchyEditingService : IHierarchyEditingService
{
    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;
    private readonly ISectorProjectionService _projection;
    private readonly NeighboursOptions _opt;
    private readonly DivisionOptions _division;

    public EfHierarchyEditingService(VipiDbContext db, IEditAuthorizationService authz,
        ISectorProjectionService projection, IOptions<NeighboursOptions> opt, IOptions<DivisionOptions> division)
    {
        _db = db;
        _authz = authz;
        _projection = projection;
        _opt = opt.Value;
        _division = division.Value;
    }

    /// <summary>Un ACC è ESTERO se il codice non inizia con un prefisso divisione. Regola pura in <see cref="HierarchyRules"/>.</summary>
    private bool IsForeignCode(string code) => HierarchyRules.IsForeignCode(code, _division.IcaoPrefixes);

    public async Task<IReadOnlyList<HierarchyNode>> LoadTreeAsync(CancellationToken ct = default)
    {
        var nodes = new List<HierarchyNode>();

        // Mappa ACC → prefisso nazione (per i padri per-nazione). Estero deciso dai prefissi divisione (non dal flag).
        var prefixByCode = await _db.Accs.AsNoTracking()
            .ToDictionaryAsync(a => a.Code, a => a.CountryPrefix, StringComparer.OrdinalIgnoreCase, ct);
        (bool F, string P) Meta(string code) =>
            (IsForeignCode(code), prefixByCode.TryGetValue(code, out var p) ? p : (code.Length >= 2 ? code[..2] : code));

        var accSectors = await _db.AccSectors.AsNoTracking()
            .OrderBy(s => s.CenterId).ThenBy(s => s.ComposePosition).ToListAsync(ct);
        foreach (var s in accSectors)
        {
            var (f, p) = Meta(s.CenterId);
            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.Acc, s.Id, s.ComposePosition,
                Label: s.ComposePosition, AccCode: s.CenterId,
                ParentCallsign: s.ParentCallsign, IsHidden: s.IsHidden, IsForeign: f, CountryPrefix: p));
        }

        var apps = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.Position != null && s.Position.ToUpper() == "APP")
            .OrderBy(s => s.AccCode).ThenBy(s => s.ComposePosition).ToListAsync(ct);
        foreach (var s in apps)
        {
            var (f, p) = Meta(s.AccCode);
            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.App, s.Id, s.ComposePosition,
                Label: s.ComposePosition, AccCode: s.AccCode,
                ParentCallsign: s.ParentCallsign, IsHidden: s.IsHidden, IsForeign: f, CountryPrefix: p));
        }

        var airports = await _db.Airports.AsNoTracking().Include(a => a.Acc)
            .OrderBy(a => a.Icao).ToListAsync(ct);
        foreach (var a in airports)
        {
            var (f, p) = Meta(a.Acc?.Code ?? "");
            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.Airport, a.Id, Callsign: null,
                Label: string.IsNullOrWhiteSpace(a.Name) ? a.Icao : $"{a.Icao} — {a.Name}",
                AccCode: a.Acc?.Code ?? "", ParentCallsign: a.ParentCallsign, IsHidden: a.IsHidden, IsForeign: f, CountryPrefix: p));
        }

        return nodes;
    }

    // Cache del set confinanti (calcolo geometrico O(N²) su poligoni densi: costoso, cambia solo dopo import/edit
    // gerarchia). Static: condiviso tra richieste/utenti (il set è globale). TTL + invalidazione esplicita su SetParent.
    private static readonly object _confiningLock = new();
    private static IReadOnlySet<string>? _confiningCache;
    private static DateTime _confiningCachedAt;
    private static readonly TimeSpan _confiningTtl = TimeSpan.FromMinutes(5);
    /// <summary>Svuota la cache del set confinanti. Chiamata da <see cref="EfSectorProjectionService"/> a fine sync
    /// (choke point di ogni mutazione catalogo) così il set non resta stantio fino al TTL dopo un import/hide.</summary>
    internal static void InvalidateConfiningCache() { lock (_confiningLock) _confiningCache = null; }

    public async Task<IReadOnlySet<string>> ListConfiningForeignCallsignsAsync(CancellationToken ct = default)
    {
        lock (_confiningLock)
            if (_confiningCache is not null && DateTime.UtcNow - _confiningCachedAt < _confiningTtl)
                return _confiningCache;

        var set = await ComputeConfiningForeignCallsignsAsync(ct);
        lock (_confiningLock) { _confiningCache = set; _confiningCachedAt = DateTime.UtcNow; }
        return set;
    }

    private async Task<IReadOnlySet<string>> ComputeConfiningForeignCallsignsAsync(CancellationToken ct = default)
    {
        var all = await _db.AccSectors.AsNoTracking()
            .Where(s => !s.IsHidden && s.RegionMapPolygon != null && s.RegionMapPolygon != "")
            .Select(s => new { s.ComposePosition, s.CenterId, s.RegionMapPolygon }).ToListAsync(ct);
        var domesticPolygons = all.Where(s => !IsForeignCode(s.CenterId)).Select(s => s.RegionMapPolygon!).ToList();
        var foreignSectors = all.Where(s => IsForeignCode(s.CenterId))
            .Select(s => (s.ComposePosition, (string?)s.RegionMapPolygon)).ToList();

        // Adiacenza estero↔domestico: regola pura testata.
        return HierarchyRules.ComputeConfiningForeignCallsigns(domesticPolygons, foreignSectors, _opt.AdjacencyThresholdNm);
    }

    public async Task SetParentAsync(HierarchyNodeKind kind, int nodeId, string? parentCallsign, CancellationToken ct = default)
    {
        parentCallsign = string.IsNullOrWhiteSpace(parentCallsign) ? null : parentCallsign.Trim();

        // 1. Risolvi il nodo figlio + il suo ACC (per l'autorizzazione) + il suo callsign (per l'anti-ciclo).
        string childAccCode;
        string? childCallsign;
        switch (kind)
        {
            case HierarchyNodeKind.Acc:
            {
                var e = await _db.AccSectors.FirstOrDefaultAsync(s => s.Id == nodeId, ct)
                    ?? throw new ValidationException("Settore ACC inesistente.");
                childAccCode = e.CenterId; childCallsign = e.ComposePosition;
                break;
            }
            case HierarchyNodeKind.App:
            {
                var e = await _db.AirportSectors.FirstOrDefaultAsync(s => s.Id == nodeId, ct)
                    ?? throw new ValidationException("Posizione APP inesistente.");
                childAccCode = e.AccCode; childCallsign = e.ComposePosition;
                break;
            }
            case HierarchyNodeKind.Airport:
            {
                var e = await _db.Airports.Include(a => a.Acc).FirstOrDefaultAsync(a => a.Id == nodeId, ct)
                    ?? throw new ValidationException("Aeroporto inesistente.");
                childAccCode = e.Acc?.Code ?? throw new ValidationException("Aeroporto senza ACC.");
                childCallsign = null;   // foglia: non referenziabile come padre
                break;
            }
            default:
                throw new ValidationException("Tipo di nodo non valido.");
        }

        // Gli ACC esteri (confinanti) non hanno grant per-ACC: l'editing della loro gerarchia è riservato agli admin.
        var isForeign = await _db.Accs.AsNoTracking()
            .Where(a => a.Code == childAccCode).Select(a => a.IsForeign).FirstOrDefaultAsync(ct);
        if (isForeign) _authz.EnsureAdmin();
        else await _authz.EnsureCanEditAccAsync(childAccCode, ct);

        // 2. Valida il padre: dev'essere un nodo interno (ACC o APP) esistente; anti-ciclo per i nodi interni.
        if (parentCallsign is not null)
        {
            var internalParents = await InternalNodeParentMapAsync(ct);   // callsign → ParentCallsign
            if (!internalParents.ContainsKey(parentCallsign))
                throw new ValidationException($"Il padre «{parentCallsign}» non è un settore ACC o APP valido.");

            if (childCallsign is not null)
            {
                if (string.Equals(parentCallsign, childCallsign, StringComparison.OrdinalIgnoreCase))
                    throw new ValidationException("Un nodo non può essere padre di sé stesso.");
                HierarchyRules.EnsureNoCycle(childCallsign, parentCallsign, internalParents);
            }
        }

        // 3. Scrivi il ParentCallsign sull'entità giusta.
        switch (kind)
        {
            case HierarchyNodeKind.Acc:
                (await _db.AccSectors.FirstAsync(s => s.Id == nodeId, ct)).ParentCallsign = parentCallsign;
                break;
            case HierarchyNodeKind.App:
                (await _db.AirportSectors.FirstAsync(s => s.Id == nodeId, ct)).ParentCallsign = parentCallsign;
                break;
            case HierarchyNodeKind.Airport:
                (await _db.Airports.FirstAsync(a => a.Id == nodeId, ct)).ParentCallsign = parentCallsign;
                break;
        }
        await _db.SaveChangesAsync(ct);

        // 4. Riproietta i Sector operativi (l'albero AoR deriva da qui). La riproiezione invalida essa stessa la
        //    cache del set confinanti (vedi InvalidateConfiningCache in EfSectorProjectionService), quindi qui non
        //    serve un'invalidazione esplicita.
        await _projection.SyncFromCatalogsAsync(ct);
    }

    /// <summary>Mappa callsign → ParentCallsign per i soli nodi interni (settori ACC + posizioni APP).</summary>
    private async Task<Dictionary<string, string?>> InternalNodeParentMapAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await _db.AccSectors.AsNoTracking()
                     .Select(s => new { s.ComposePosition, s.ParentCallsign }).ToListAsync(ct))
            map[s.ComposePosition] = s.ParentCallsign;
        foreach (var s in await _db.AirportSectors.AsNoTracking()
                     .Where(s => s.Position != null && s.Position.ToUpper() == "APP")
                     .Select(s => new { s.ComposePosition, s.ParentCallsign }).ToListAsync(ct))
            map[s.ComposePosition] = s.ParentCallsign;
        return map;
    }
}
