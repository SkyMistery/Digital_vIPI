using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <inheritdoc cref="ISidImporter"/>
public sealed class SidImporter : ISidImporter
{
    private readonly ISidProvider _provider;
    private readonly IAirportRepository _repo;
    private readonly IImportPolicyStore _policy;
    private readonly IAiracService _airac;

    public SidImporter(ISidProvider provider, IAirportRepository repo, IImportPolicyStore policy, IAiracService airac)
    {
        _provider = provider;
        _repo = repo;
        _policy = policy;
        _airac = airac;
    }

    public async Task<int> ImportAsync(string icao, CancellationToken ct = default)
    {
        icao = icao.Trim().ToUpperInvariant();
        var policy = await _policy.GetAsync(ct);
        if (!policy.IsImported(ImportCategory.Sids)) return 0;   // categoria disattivata: non toccare le SID

        var source = await _provider.GetSidsAsync(icao, ct);
        if (source.Count == 0) return 0;                          // nessun file/righe: non azzerare le importate esistenti

        var cycle = _airac.GetCycle(DateTime.UtcNow);
        var rows = source.Select(s => new ImportedSid(
            Runway: s.Runway, Fix: s.Fix, Name: s.Name, Transition: s.Transition,
            Type: s.Type, StableKey: s.StableKey, NeedsFixReview: s.NeedsFixReview)).ToList();

        await _repo.ReplaceImportedSidsAsync(icao, rows, cycle, ct);
        return rows.Count;
    }
}
