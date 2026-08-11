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

    public async Task MovePointToEndAsync(string accCode, int pointId, bool top, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.MovePointToEndAsync(accCode, pointId, top, ct);
    }

    public async Task<int> AddVariantAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        return await _repo.AddVariantAsync(accCode, pointId, ct);
    }

    public async Task DetachVariantAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        await _repo.DetachVariantAsync(accCode, pointId, ct);
    }

    // Validazione SOFT: solo i campi strutturali indispensabili. Il CoP fuori whitelist è un warning di UI, non un blocco.
    private static void ValidateFlow(TransferFlowInput i)
    {
        if (i.OwningSectorId <= 0) throw new ValidationException("Il flusso deve riferirsi a un settore proprio.");
        // Arrivi/Partenze sono legati a un aeroporto (destinazione/origine): senza, la frase è orfana e la
        // derivazione li scarta. Sorvoli/VFR/Altro invece reggono senza aeroporto.
        if (i.Kind is Domain.TransferFlowKind.Arrival or Domain.TransferFlowKind.Departure && string.IsNullOrWhiteSpace(i.AirportIcao))
            throw new ValidationException("Arrivi e Partenze richiedono un aeroporto. Per i flussi senza aeroporto usa Sorvoli/VFR/Altro.");
    }

    private static void ValidatePoint(TransferPointInput i)
    {
        if (i.LevelConstraint != Domain.LevelConstraint.Special && i.LevelValue is null && string.IsNullOrWhiteSpace(i.Cop))
            throw new ValidationException("Indica almeno il CoP o un livello.");
        // Le tre dimensioni condizione (pista/area/personalizzata) sono tutte opzionali e indipendenti: nessun vincolo.

        // Faccetta trasferimento: il tipo «punto» senza etichetta non dice dove, e resterebbe muto nella frase.
        // «Confine dell'AoR» invece si descrive da sé, e l'etichetta lì non serve.
        if (i.HandoffKind is Domain.TransferHandoffKind.Point or Domain.TransferHandoffKind.Custom
            && string.IsNullOrWhiteSpace(i.HandoffLabel))
            throw new ValidationException("Indica dove avviene il trasferimento (punto o testo).");
        if (i.CommsHandoffKind is Domain.TransferHandoffKind.Point or Domain.TransferHandoffKind.Custom
            && string.IsNullOrWhiteSpace(i.CommsHandoffLabel))
            throw new ValidationException("Indica dove passano le comunicazioni (punto o testo).");

        // Velocità: un vincolo senza numero non è una restrizione, è un campo lasciato a metà.
        if (i.SpeedConstraint != Domain.SpeedConstraint.Unspecified && i.SpeedValue is null)
            throw new ValidationException("Indica il valore della velocità, o togli il vincolo.");

        // «Negli altri casi» è il complemento delle condizioni delle sorelle: portarne una sarebbe una contraddizione.
        if (i.IsOtherwise && (!string.IsNullOrWhiteSpace(i.ConditionLabel)
                              || !string.IsNullOrWhiteSpace(i.ConditionAreaLabel)
                              || !string.IsNullOrWhiteSpace(i.ConditionCustomLabel)))
            throw new ValidationException("La riga «negli altri casi» non porta condizioni: è il caso che resta.");
    }
}
