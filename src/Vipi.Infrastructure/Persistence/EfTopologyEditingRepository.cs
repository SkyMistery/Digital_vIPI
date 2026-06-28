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

        var sectors = await _db.Sectors.Where(s => s.FirId == fid)
            .OrderBy(s => s.Callsign)
            .Select(s => new { s.Id, s.Callsign, s.ParentSectorId }).ToListAsync(ct);
        var refs = sectors.Select(s => new SectorRef(s.Id, s.Callsign)).ToList();
        var byId = sectors.ToDictionary(s => s.Id, s => s.Callsign);

        var rules = await _db.UnificationRules.Where(r => r.FirId == fid)
            .OrderBy(r => r.Priority)
            .Select(r => new RuleRow
            {
                Id = r.Id, Name = r.Name, Priority = r.Priority,
                ConditionJson = r.ConditionJson, AssignmentJson = r.AssignmentJson, IsActive = r.IsActive,
            }).ToListAsync(ct);

        // Contenimento derivato da Sector.ParentSectorId (Id riga = id del settore figlio).
        var hierarchy = sectors
            .Where(s => s.ParentSectorId is int pid && byId.ContainsKey(pid))
            .Select(s => new HierarchyRow
            {
                ChildSectorId = s.Id, ChildCallsign = s.Callsign,
                ParentSectorId = s.ParentSectorId!.Value, ParentCallsign = byId[s.ParentSectorId!.Value],
            }).ToList();

        return new TopologyEditData { FirId = fid, Sectors = refs, Rules = rules, Hierarchy = hierarchy };
    }

    public async Task<FirVocabulary?> GetVocabularyAsync(string firCode, CancellationToken ct = default)
    {
        var firId = await FirIdAsync(firCode, ct);
        if (firId is not int fid) return null;

        // Settore == posizione: il vocabolario è l'insieme dei callsign noti (qualsiasi FIR, per ammettere i neighbour).
        var callsigns = await _db.Sectors.Select(s => s.Callsign).ToListAsync(ct);

        return new FirVocabulary
        {
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

    public async Task SetParentAsync(string firCode, int childSectorId, int? parentSectorId, CancellationToken ct = default)
    {
        var fid = await FirIdAsync(firCode, ct) ?? throw new InvalidOperationException($"FIR {firCode} inesistente.");
        if (parentSectorId == childSectorId)
            throw new InvalidOperationException("Un settore non può essere padre di sé stesso.");

        var child = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == childSectorId && s.FirId == fid, ct)
            ?? throw new InvalidOperationException("Settore figlio non appartiene alla FIR.");

        if (parentSectorId is int pid)
        {
            if (!await _db.Sectors.AnyAsync(s => s.Id == pid && s.FirId == fid, ct))
                throw new InvalidOperationException("Settore padre non appartiene alla FIR.");
            await EnsureNoCycleAsync(childSectorId, pid, ct);
        }

        child.ParentSectorId = parentSectorId;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Rifiuta il padre se figlio è già (transitivamente) antenato del padre proposto (anti-ciclo).</summary>
    private async Task EnsureNoCycleAsync(int childSectorId, int proposedParentId, CancellationToken ct)
    {
        var current = (int?)proposedParentId;
        var guard = new HashSet<int>();
        while (current is int id && guard.Add(id))
        {
            if (id == childSectorId)
                throw new InvalidOperationException("Contenimento non valido: creerebbe un ciclo.");
            current = await _db.Sectors.Where(s => s.Id == id).Select(s => s.ParentSectorId).FirstOrDefaultAsync(ct);
        }
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
