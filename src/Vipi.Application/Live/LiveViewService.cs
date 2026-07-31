using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;

namespace Vipi.Application.Live;

/// <summary>
/// Punto d'ingresso della vista live: da un callsign al modello uniforme, passando dal registry dei descrittori.
/// La vista è legata all'ENTE, non a un documento (vedi memoria live-view-design): qui non si consulta nessun
/// documento per decidere se la pagina esiste — basta che il settore sia nei cataloghi.
/// </summary>
public interface ILiveViewService
{
    /// <summary>Callsign con cui l'utente corrente risulta connesso su IVAO adesso, o null (anonimo/offline).</summary>
    string? MyCallsign();

    /// <summary>Snapshot ATC corrente (serve anche allo stato d'attesa: elenco online + età del dato).</summary>
    OnlineAtcSnapshot Snapshot();

    /// <summary>Compone la vista di una postazione. <c>Found=false</c> se il callsign non è nei cataloghi.</summary>
    Task<LiveViewResult> BuildAsync(string callsign, CancellationToken ct = default);
}

/// <inheritdoc cref="ILiveViewService"/>
public sealed class LiveViewService : ILiveViewService
{
    private readonly IStationResolver _stations;
    private readonly IStructureEditingService _structure;
    private readonly ITopologyProvider _topology;
    private readonly IOnlineAtcProvider _online;
    private readonly ICurrentUserProvider _users;
    private readonly ILiveStationRegistry _registry;

    public LiveViewService(IStationResolver stations, IStructureEditingService structure,
        ITopologyProvider topology, IOnlineAtcProvider online, ICurrentUserProvider users,
        ILiveStationRegistry registry)
    {
        _stations = stations;
        _structure = structure;
        _topology = topology;
        _online = online;
        _users = users;
        _registry = registry;
    }

    public OnlineAtcSnapshot Snapshot() => _online.GetCurrent();

    public string? MyCallsign()
    {
        if (_users.Get() is not { } user) return null;
        return _online.GetCurrent().Details.FirstOrDefault(d => d.UserId == user.UserId)?.Callsign;
    }

    public async Task<LiveViewResult> BuildAsync(string callsign, CancellationToken ct = default)
    {
        callsign = (callsign ?? "").Trim().ToUpperInvariant();
        if (callsign.Length == 0) return LiveViewResult.NotFound(callsign);

        // ACC di competenza dal callsign (per testa = codice ACC, oppure ICAO di un aeroporto).
        var acc = _stations.ResolveByCallsign(callsign);
        if (acc is null) return LiveViewResult.NotFound(callsign);

        var structure = await _structure.LoadAsync(acc.Code, ct);
        var sector = structure?.Sectors.FirstOrDefault(s =>
            string.Equals(s.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
        if (structure is null || sector is null) return LiveViewResult.NotFound(callsign);

        var topology = await _topology.BuildByAccCodeAsync(acc.Code, ct);
        if (topology is null) return LiveViewResult.NotFound(callsign);

        var snapshot = _online.GetCurrent();
        var ctx = new LiveStationContext(callsign, sector, acc, structure, topology, snapshot.Callsigns);

        var kind = _registry.For(ctx);
        if (kind is null) return LiveViewResult.NotFound(callsign);

        return new LiveViewResult(await kind.BuildAsync(ctx, ct), callsign);
    }
}
