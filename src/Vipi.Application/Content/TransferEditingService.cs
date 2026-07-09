using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <inheritdoc cref="ITransferService"/>
public sealed class TransferService : ITransferService
{
    private readonly ITransferRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly ITopologyProvider _topology;

    public TransferService(ITransferRepository repo, IEditAuthorizationService authz, ITopologyProvider topology)
    {
        _repo = repo;
        _authz = authz;
        _topology = topology;
    }

    public Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default) =>
        _repo.ListFlowsByAccAsync(accCode, ct);

    public async Task<IReadOnlyList<ResolvedTransferFlow>> ResolveForAccAsync(
        string accCode, IReadOnlySet<string> online, CancellationToken ct = default)
    {
        var flows = await _repo.ListFlowsByAccAsync(accCode, ct);
        var topo = await _topology.BuildGlobalAsync(ct);

        // Catena di candidati di un settore: sé stesso + antenati di copertura (cross-ACC), in ordine di priorità.
        IReadOnlyList<string> Chain(string? callsign) =>
            string.IsNullOrWhiteSpace(callsign)
                ? Array.Empty<string>()
                : new[] { callsign }.Concat(topo.Ancestors(callsign)).ToList();

        return flows.Select(f =>
        {
            var ownerHit = TransferOnlineResolver.FirstOnline(Chain(f.OwningSectorCallsign), online);
            var points = f.Points.Select(p =>
            {
                var (handler, isOnline) = TransferOnlineResolver.Resolve(Chain(p.NextSectorCallsign), online);
                return new ResolvedTransferPoint { Point = p, ResolvedHandler = handler, IsOnline = isOnline };
            }).ToList();

            return new ResolvedTransferFlow
            {
                Flow = f,
                ResolvedOwnerCallsign = ownerHit ?? f.OwningSectorCallsign,
                OwnerOnline = ownerHit is not null,
                Points = points,
            };
        }).ToList();
    }

    public async Task<int> AddFlowAsync(string accCode, TransferFlowInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidateFlow(input);
        return await _repo.AddFlowAsync(accCode, input, ct);
    }

    public async Task UpdateFlowAsync(string accCode, int flowId, TransferFlowInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidateFlow(input);
        await _repo.UpdateFlowAsync(accCode, flowId, input, ct);
    }

    public async Task DeleteFlowAsync(string accCode, int flowId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeleteFlowAsync(accCode, flowId, ct);
    }

    public async Task<int> AddPointAsync(string accCode, int flowId, TransferPointInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidatePoint(input);
        return await _repo.AddPointAsync(accCode, flowId, input, ct);
    }

    public async Task UpdatePointAsync(string accCode, int pointId, TransferPointInput input, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        ValidatePoint(input);
        await _repo.UpdatePointAsync(accCode, pointId, input, ct);
    }

    public async Task DeletePointAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DeletePointAsync(accCode, pointId, ct);
    }

    public async Task MovePointAsync(string accCode, int pointId, bool up, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.MovePointAsync(accCode, pointId, up, ct);
    }

    // Validazione SOFT: solo i campi strutturali indispensabili. Il CoP fuori whitelist è un warning di UI, non un blocco.
    private static void ValidateFlow(TransferFlowInput i)
    {
        if (i.OwningSectorId <= 0) throw new ValidationException("Il flusso deve riferirsi a un settore proprio.");
    }

    private static void ValidatePoint(TransferPointInput i)
    {
        if (i.LevelConstraint != Domain.LevelConstraint.Special && i.LevelValue is null && string.IsNullOrWhiteSpace(i.Cop))
            throw new ValidationException("Indica almeno il CoP o un livello.");
    }
}
