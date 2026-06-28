using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;

namespace Vipi.Application.Aor;

/// <summary>Use-case editing topologia (ACC-scoped via <see cref="IEditAuthorizationService"/>) + validazione hard delle regole.</summary>
public interface ITopologyEditingService
{
    Task<TopologyEditData?> LoadAsync(string accCode, CancellationToken ct = default);
    Task<int> AddRuleAsync(string accCode, string name, int priority, string conditionJson, string assignmentJson, CancellationToken ct = default);
    Task SetRuleActiveAsync(string accCode, int ruleId, bool active, CancellationToken ct = default);
    Task DeleteRuleAsync(string accCode, int ruleId, CancellationToken ct = default);
    /// <summary>Imposta il padre (contenimento) di un settore figlio. parentSectorId null = radice.</summary>
    Task SetParentAsync(string accCode, int childSectorId, int? parentSectorId, CancellationToken ct = default);
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

    // Lettura aperta a chi apre la pagina (la pagina stessa è in area admin/editor); le scritture sono ACC-gated.
    public Task<TopologyEditData?> LoadAsync(string accCode, CancellationToken ct = default) =>
        _repo.LoadAsync(accCode, ct);

    public async Task<int> AddRuleAsync(string accCode, string name, int priority, string conditionJson, string assignmentJson, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await ValidateRuleAsync(accCode, conditionJson, assignmentJson, ct);
        return await _repo.AddRuleAsync(accCode, name, priority, conditionJson, assignmentJson, ct);
    }

    public async Task SetRuleActiveAsync(string accCode, int ruleId, bool active, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.SetRuleActiveAsync(accCode, ruleId, active, ct);
    }

    public async Task DeleteRuleAsync(string accCode, int ruleId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeleteRuleAsync(accCode, ruleId, ct);
    }

    public async Task SetParentAsync(string accCode, int childSectorId, int? parentSectorId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        if (parentSectorId == childSectorId) throw new ValidationException("Un settore non può essere padre di sé stesso.");
        await _repo.SetParentAsync(accCode, childSectorId, parentSectorId, ct);
    }

    /// <summary>Validazione hard: settore assegnato e owner (callsign) ∈ settori noti.</summary>
    private async Task ValidateRuleAsync(string accCode, string conditionJson, string assignmentJson, CancellationToken ct)
    {
        var vocab = await _repo.GetVocabularyAsync(accCode, ct)
            ?? throw new ValidationException($"ACC {accCode} inesistente.");

        var unknownSectors = new List<string>();
        var unknownCallsigns = new List<string>();

        // assignment: { sectorKey : ownerCallsign }
        try
        {
            var assign = JsonSerializer.Deserialize<Dictionary<string, string>>(
                string.IsNullOrWhiteSpace(assignmentJson) ? "{}" : assignmentJson) ?? new();
            foreach (var (sector, owner) in assign)
            {
                if (!vocab.Callsigns.Contains(sector)) unknownSectors.Add(sector);
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
