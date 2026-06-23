using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="ITopologyEditingRepository"/> (regole + gerarchia, FIR-scoped).</summary>
public sealed class EfTopologyEditingRepository : ITopologyEditingRepository
{
    private readonly VipiDbContext _db;
    public EfTopologyEditingRepository(VipiDbContext db) => _db = db;

    public async Task<TopologyEditData?> LoadAsync(string firCode, CancellationToken ct = default)
    {
        var firId = await FirIdAsync(firCode, ct);
        if (firId is not int fid) return null;

        var positions = await _db.Positions.Where(p => p.FirId == fid)
            .OrderBy(p => p.Callsign)
            .Select(p => new PositionRef(p.Id, p.Callsign)).ToListAsync(ct);
        var byId = positions.ToDictionary(p => p.Id, p => p.Callsign);

        var rules = await _db.UnificationRules.Where(r => r.FirId == fid)
            .OrderBy(r => r.Priority)
            .Select(r => new RuleRow
            {
                Id = r.Id, Name = r.Name, Priority = r.Priority,
                ConditionJson = r.ConditionJson, AssignmentJson = r.AssignmentJson, IsActive = r.IsActive,
            }).ToListAsync(ct);

        var hier = await _db.HierarchyRelations.Where(h => h.FirId == fid).ToListAsync(ct);
        var hierarchy = hier
            .Where(h => byId.ContainsKey(h.ParentPositionId) && byId.ContainsKey(h.ChildPositionId))
            .Select(h => new HierarchyRow
            {
                Id = h.Id,
                ParentPositionId = h.ParentPositionId, ParentCallsign = byId[h.ParentPositionId],
                ChildPositionId = h.ChildPositionId, ChildCallsign = byId[h.ChildPositionId],
            }).ToList();

        return new TopologyEditData { FirId = fid, Positions = positions, Rules = rules, Hierarchy = hierarchy };
    }

    public async Task<FirVocabulary?> GetVocabularyAsync(string firCode, CancellationToken ct = default)
    {
        var firId = await FirIdAsync(firCode, ct);
        if (firId is not int fid) return null;

        var sectors = await _db.Sectors.Where(s => s.FirId == fid).Select(s => s.Key).ToListAsync(ct);
        var callsigns = await _db.Positions.Select(p => p.Callsign).ToListAsync(ct); // qualsiasi FIR (ammette neighbour)

        return new FirVocabulary
        {
            SectorKeys = sectors.ToHashSet(StringComparer.OrdinalIgnoreCase),
            Callsigns = callsigns.ToHashSet(StringComparer.OrdinalIgnoreCase),
        };
    }

    public async Task<int> AddRuleAsync(string firCode, string name, int priority, string conditionJson, string assignmentJson, CancellationToken ct = default)
    {
        var fid = await FirIdAsync(firCode, ct) ?? throw new InvalidOperationException($"FIR {firCode} inesistente.");
        var rule = new UnificationRule
        {
            FirId = fid,
            Name = string.IsNullOrWhiteSpace(name) ? "Nuova regola" : name.Trim(),
            Priority = priority,
            ConditionJson = string.IsNullOrWhiteSpace(conditionJson) ? "{}" : conditionJson,
            AssignmentJson = string.IsNullOrWhiteSpace(assignmentJson) ? "{}" : assignmentJson,
            IsActive = true,
        };
        _db.UnificationRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return rule.Id;
    }

    public async Task SetRuleActiveAsync(string firCode, int ruleId, bool active, CancellationToken ct = default)
    {
        var rule = await RuleInFirAsync(firCode, ruleId, ct);
        rule.IsActive = active;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteRuleAsync(string firCode, int ruleId, CancellationToken ct = default)
    {
        var rule = await RuleInFirAsync(firCode, ruleId, ct);
        _db.UnificationRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AddHierarchyAsync(string firCode, int parentPositionId, int childPositionId, CancellationToken ct = default)
    {
        var fid = await FirIdAsync(firCode, ct) ?? throw new InvalidOperationException($"FIR {firCode} inesistente.");
        if (parentPositionId == childPositionId)
            throw new InvalidOperationException("Padre e figlio non possono coincidere.");
        var exists = await _db.HierarchyRelations.AnyAsync(
            h => h.ParentPositionId == parentPositionId && h.ChildPositionId == childPositionId, ct);
        if (exists) throw new InvalidOperationException("Relazione già esistente.");

        var rel = new HierarchyRelation { FirId = fid, ParentPositionId = parentPositionId, ChildPositionId = childPositionId };
        _db.HierarchyRelations.Add(rel);
        await _db.SaveChangesAsync(ct);
        return rel.Id;
    }

    public async Task DeleteHierarchyAsync(string firCode, int relationId, CancellationToken ct = default)
    {
        var fid = await FirIdAsync(firCode, ct) ?? throw new InvalidOperationException($"FIR {firCode} inesistente.");
        var rel = await _db.HierarchyRelations.FirstOrDefaultAsync(h => h.Id == relationId && h.FirId == fid, ct);
        if (rel is null) return;
        _db.HierarchyRelations.Remove(rel);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<int?> FirIdAsync(string firCode, CancellationToken ct) =>
        await _db.Firs.Where(f => f.Code == firCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);

    private async Task<UnificationRule> RuleInFirAsync(string firCode, int ruleId, CancellationToken ct)
    {
        var fid = await FirIdAsync(firCode, ct) ?? throw new InvalidOperationException($"FIR {firCode} inesistente.");
        return await _db.UnificationRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.FirId == fid, ct)
            ?? throw new InvalidOperationException($"Regola {ruleId} non appartiene alla FIR {firCode}.");
    }
}
