using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Aor;

/// <summary>
/// Costruisce la <see cref="Topology"/> pura (Application) leggendo l'anagrafica di una FIR dal DB.
/// Qui vive la conoscenza del formato JSON delle <c>UnificationRule</c>; la logica AoR resta DB-agnostica.
/// Implementa <see cref="ITopologyProvider"/> (porta usata da Application/UI).
/// </summary>
public sealed class TopologyBuilder : ITopologyProvider
{
    private readonly VipiDbContext _db;

    public TopologyBuilder(VipiDbContext db) => _db = db;

    public async Task<Topology?> BuildByFirCodeAsync(string firCode, CancellationToken ct = default)
    {
        var firId = await _db.Firs.Where(f => f.Code == firCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);
        return firId is int id ? await BuildAsync(id, ct) : null;
    }

    public async Task<Topology> BuildAsync(int firId, CancellationToken ct = default)
    {
        var positions = await _db.Positions.Where(p => p.FirId == firId)
            .Select(p => new { p.Id, p.Callsign }).ToListAsync(ct);
        var byId = positions.ToDictionary(p => p.Id, p => p.Callsign);

        var posSectors = await (
            from ps in _db.PositionSectors
            join s in _db.Sectors on ps.SectorId equals s.Id
            where s.FirId == firId
            select new { ps.PositionId, s.Key }).ToListAsync(ct);

        var defaultSectors = posSectors
            .Where(x => byId.ContainsKey(x.PositionId))
            .GroupBy(x => byId[x.PositionId])
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.Key).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var relations = await _db.HierarchyRelations.Where(h => h.FirId == firId)
            .Select(h => new { h.ParentPositionId, h.ChildPositionId }).ToListAsync(ct);

        var parent = relations
            .Where(r => byId.ContainsKey(r.ParentPositionId) && byId.ContainsKey(r.ChildPositionId))
            .ToDictionary(r => byId[r.ChildPositionId], r => byId[r.ParentPositionId],
                StringComparer.OrdinalIgnoreCase);

        var rules = await _db.UnificationRules.Where(u => u.FirId == firId && u.IsActive)
            .OrderBy(u => u.Priority).ToListAsync(ct);

        var ruleSpecs = rules.Select(r => new UnificationRuleSpec(
            r.Name,
            r.Priority,
            ParseRequiredOnline(r.ConditionJson),
            ParseAssignment(r.AssignmentJson))).ToList();

        return new Topology
        {
            DefaultSectors = defaultSectors,
            Parent = parent,
            Rules = ruleSpecs,
        };
    }

    private static IReadOnlyCollection<string> ParseRequiredOnline(string json)
    {
        // Forma attesa: {"online":["LIMM_WS5_CTR", ...]}
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("online", out var arr) && arr.ValueKind == JsonValueKind.Array)
                return arr.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToList();
        }
        catch (JsonException) { /* regola malformata → condizione vuota (mai attivata) */ }
        return Array.Empty<string>();
    }

    private static IReadOnlyDictionary<string, string> ParseAssignment(string json)
    {
        // Forma attesa: {"WS2":"LIMM_WS2_CTR","ES2":"LIMM_WS2_CTR", ...}
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (map is not null)
                return new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
