using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;

namespace Vipi.Application.Aor;

/// <summary>Use-case editing topologia (FIR-scoped via <see cref="IEditAuthorizationService"/>) + validazione hard delle regole.</summary>
public interface ITopologyEditingService
{
    Task<TopologyEditData?> LoadAsync(string firCode, CancellationToken ct = default);
    Task<int> AddRuleAsync(string firCode, string name, int priority, string conditionJson, string assignmentJson, CancellationToken ct = default);
    Task SetRuleActiveAsync(string firCode, int ruleId, bool active, CancellationToken ct = default);
    Task DeleteRuleAsync(string firCode, int ruleId, CancellationToken ct = default);
    Task<int> AddHierarchyAsync(string firCode, int parentPositionId, int childPositionId, CancellationToken ct = default);
    Task DeleteHierarchyAsync(string firCode, int relationId, CancellationToken ct = default);
}

/// <summary>Errore di validazione semantica (input rifiutato con motivo leggibile).</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

/// <inheritdoc cref="ITopologyEditingService"/>
public sealed class TopologyEditingService : ITopologyEditingService
{
    private readonly ITopologyEditingRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public TopologyEditingService(ITopologyEditingRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    // Lettura aperta a chi apre la pagina (la pagina stessa è in area admin/editor); le scritture sono FIR-gated.
    public Task<TopologyEditData?> LoadAsync(string firCode, CancellationToken ct = default) =>
        _repo.LoadAsync(firCode, ct);

    public async Task<int> AddRuleAsync(string firCode, string name, int priority, string conditionJson, string assignmentJson, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await ValidateRuleAsync(firCode, conditionJson, assignmentJson, ct);
        return await _repo.AddRuleAsync(firCode, name, priority, conditionJson, assignmentJson, ct);
    }

    public async Task SetRuleActiveAsync(string firCode, int ruleId, bool active, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.SetRuleActiveAsync(firCode, ruleId, active, ct);
    }

    public async Task DeleteRuleAsync(string firCode, int ruleId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.DeleteRuleAsync(firCode, ruleId, ct);
    }

    public async Task<int> AddHierarchyAsync(string firCode, int parentPositionId, int childPositionId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        return await _repo.AddHierarchyAsync(firCode, parentPositionId, childPositionId, ct);
    }

    public async Task DeleteHierarchyAsync(string firCode, int relationId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditFirAsync(firCode, ct);
        await _repo.DeleteHierarchyAsync(firCode, relationId, ct);
    }

    /// <summary>Validazione hard: sectorKey assegnati ∈ settori FIR; callsign (online + owner) ∈ posizioni note.</summary>
    private async Task ValidateRuleAsync(string firCode, string conditionJson, string assignmentJson, CancellationToken ct)
    {
        var vocab = await _repo.GetVocabularyAsync(firCode, ct)
            ?? throw new ValidationException($"FIR {firCode} inesistente.");

        var unknownSectors = new List<string>();
        var unknownCallsigns = new List<string>();

        // assignment: { sectorKey : ownerCallsign }
        try
        {
            var assign = JsonSerializer.Deserialize<Dictionary<string, string>>(
                string.IsNullOrWhiteSpace(assignmentJson) ? "{}" : assignmentJson) ?? new();
            foreach (var (sector, owner) in assign)
            {
                if (!vocab.SectorKeys.Contains(sector)) unknownSectors.Add(sector);
                if (!string.IsNullOrWhiteSpace(owner) && !vocab.Callsigns.Contains(owner)) unknownCallsigns.Add(owner);
            }
        }
        catch (JsonException) { throw new ValidationException("Assegnazione JSON non valida."); }

        // condition: { "online": [callsign, ...] }
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(conditionJson) ? "{}" : conditionJson);
            if (doc.RootElement.TryGetProperty("online", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var e in arr.EnumerateArray())
                {
                    var cs = e.GetString();
                    if (!string.IsNullOrWhiteSpace(cs) && !vocab.Callsigns.Contains(cs)) unknownCallsigns.Add(cs);
                }
        }
        catch (JsonException) { throw new ValidationException("Condizione JSON non valida."); }

        if (unknownSectors.Count > 0 || unknownCallsigns.Count > 0)
        {
            var parts = new List<string>();
            if (unknownSectors.Count > 0) parts.Add($"settori inesistenti: {string.Join(", ", unknownSectors)}");
            if (unknownCallsigns.Count > 0) parts.Add($"callsign sconosciuti: {string.Join(", ", unknownCallsigns)}");
            throw new ValidationException("Regola non valida — " + string.Join(" · ", parts) + ".");
        }
    }
}
