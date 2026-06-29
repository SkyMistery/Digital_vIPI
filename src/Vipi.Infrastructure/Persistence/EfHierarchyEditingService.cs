using Microsoft.EntityFrameworkCore;
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

    public EfHierarchyEditingService(VipiDbContext db, IEditAuthorizationService authz, ISectorProjectionService projection)
    {
        _db = db;
        _authz = authz;
        _projection = projection;
    }

    public async Task<IReadOnlyList<HierarchyNode>> LoadTreeAsync(CancellationToken ct = default)
    {
        var nodes = new List<HierarchyNode>();

        var accSectors = await _db.AccSectors.AsNoTracking()
            .OrderBy(s => s.CenterId).ThenBy(s => s.ComposePosition).ToListAsync(ct);
        foreach (var s in accSectors)
            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.Acc, s.Id, s.ComposePosition,
                Label: s.ComposePosition, AccCode: s.CenterId,
                ParentCallsign: s.ParentCallsign, IsHidden: s.IsHidden));

        var apps = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.Position != null && s.Position.ToUpper() == "APP")
            .OrderBy(s => s.AccCode).ThenBy(s => s.ComposePosition).ToListAsync(ct);
        foreach (var s in apps)
            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.App, s.Id, s.ComposePosition,
                Label: s.ComposePosition, AccCode: s.AccCode,
                ParentCallsign: s.ParentCallsign, IsHidden: s.IsHidden));

        var airports = await _db.Airports.AsNoTracking().Include(a => a.Acc)
            .OrderBy(a => a.Icao).ToListAsync(ct);
        foreach (var a in airports)
            nodes.Add(new HierarchyNode(
                HierarchyNodeKind.Airport, a.Id, Callsign: null,
                Label: string.IsNullOrWhiteSpace(a.Name) ? a.Icao : $"{a.Icao} — {a.Name}",
                AccCode: a.Acc?.Code ?? "", ParentCallsign: a.ParentCallsign, IsHidden: a.IsHidden));

        return nodes;
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

        await _authz.EnsureCanEditAccAsync(childAccCode, ct);

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
                EnsureNoCycle(childCallsign, parentCallsign, internalParents);
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

        // 4. Riproietta i Sector operativi (l'albero AoR deriva da qui).
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

    /// <summary>Rifiuta se il figlio è già (transitivamente) antenato del padre proposto (anti-ciclo).</summary>
    private static void EnsureNoCycle(string childCallsign, string proposedParent, Dictionary<string, string?> parents)
    {
        var current = (string?)proposedParent;
        var guard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (current is not null && guard.Add(current))
        {
            if (string.Equals(current, childCallsign, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Gerarchia non valida: creerebbe un ciclo.");
            parents.TryGetValue(current, out current);
        }
    }
}
