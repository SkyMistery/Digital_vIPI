using Vipi.Application.Abstractions;
using Vipi.AuroraBridge.Contracts;

namespace Vipi.Application.Content;

/// <summary>Use-case del bridge Aurora: risolve il livello di trasferimento per un volo selezionato in Aurora.
/// Sola lettura, nessuna autorizzazione: espone dati già pubblici nei documenti.</summary>
public interface ITransferMatchService
{
    Task<TransferResolveResponse> ResolveAsync(TransferResolveRequest request, CancellationToken ct = default);
}

/// <summary>
/// Orchestrazione sottile attorno a <see cref="TransferMatcher"/>: carica ACC, flussi, topologia globale e
/// ATC online, poi delega tutta la logica al matcher puro. Qui dentro non c'è nessuna decisione di merito.
/// </summary>
public sealed class TransferMatchService : ITransferMatchService
{
    private readonly IAgreementService _agreements;
    private readonly ITopologyProvider _topology;
    private readonly IStationResolver _stations;
    private readonly IOnlineAtcProvider _online;
    private readonly TransferMatchOptions _options;

    public TransferMatchService(
        IAgreementService agreements,
        ITopologyProvider topology,
        IStationResolver stations,
        IOnlineAtcProvider online,
        TransferMatchOptions? options = null)
    {
        _agreements = agreements;
        _topology = topology;
        _stations = stations;
        _online = online;
        _options = options ?? new TransferMatchOptions();
    }

    public async Task<TransferResolveResponse> ResolveAsync(TransferResolveRequest request, CancellationToken ct = default)
    {
        var snapshot = _online.GetCurrent();
        var acc = _stations.ResolveByCallsign(request.OwnerCallsign ?? "");
        var topo = await _topology.BuildGlobalAsync(ct);

        if (acc is null)
        {
            return new TransferResolveResponse
            {
                AsOf = DateTimeOffset.UtcNow,
                OnlineAsOf = snapshot.AsOf,
                Warnings = { $"Callsign «{request.OwnerCallsign}» non riconducibile a nessuna ACC del sito." },
            };
        }

        // Le righe piatte proiettate dagli accordi: il matcher le legge come le ha sempre lette — due
        // varianti SONO due candidati distinti, ed e' la lettura giusta per lui.
        var flows = await _agreements.ListFlowsByAccAsync(acc.Code, ct);

        return TransferMatcher.Match(
            request, flows, topo, snapshot.Callsigns, acc.Code,
            snapshot.AsOf, DateTimeOffset.UtcNow, _options);
    }
}
