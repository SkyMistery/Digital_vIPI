using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;

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

    /// <summary>
    /// Compone la vista di una postazione. <c>Found=false</c> se il callsign non è nei cataloghi,
    /// <c>Denied=true</c> se è la postazione di qualcun altro e chi guarda non è almeno DivisionStaff.
    /// </summary>
    Task<LiveViewResult> BuildAsync(string callsign, CancellationToken ct = default);

    /// <summary>
    /// Le postazioni fra cui si può scegliere. <b>Solo DivisionStaff in su</b>: per tutti gli altri la
    /// vista live è la propria postazione e basta, quindi non c'è niente da scegliere.
    /// <para>⚠️ Da chiamare <b>a richiesta</b>, non a ogni tick live: è una lettura dal database, e la
    /// vista live si ricompone a ogni giro del poller.</para>
    /// </summary>
    Task<IReadOnlyList<LiveStationOption>> ListStationsAsync(CancellationToken ct = default);
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
    private readonly IEditAuthorizationService _authz;
    private readonly IStructureEditingRepository _sectors;
    private readonly IAtcUnitRepository? _enti;
    private readonly Vipi.Application.EventKits.AccountEventoRegistro? _account;

    public LiveViewService(IStationResolver stations, IStructureEditingService structure,
        ITopologyProvider topology, IOnlineAtcProvider online, ICurrentUserProvider users,
        ILiveStationRegistry registry, IEditAuthorizationService authz,
        IStructureEditingRepository sectors, IAtcUnitRepository? enti = null,
        Vipi.Application.EventKits.AccountEventoRegistro? account = null)
    {
        _account = account;
        _enti = enti;
        _stations = stations;
        _structure = structure;
        _topology = topology;
        _online = online;
        _users = users;
        _registry = registry;
        _authz = authz;
        _sectors = sectors;
    }

    public OnlineAtcSnapshot Snapshot() => _online.GetCurrent();

    /// <summary>
    /// ⚠️ Prima l'ACCOUNT DELL'EVENTO, se chi guarda ne sta usando uno (carta 2026-10-01-account-evento.md): durante un
    /// evento si controlla con un VID dato dall'organizzazione, e il proprio non è connesso. Se quel VID non è più
    /// online si torna al proprio — senza dimenticare la scelta: alla riconnessione la vista riprende da sé.
    /// </summary>
    public string? MyCallsign()
    {
        if (_users.Get() is not { } user) return null;
        var dettagli = _online.GetCurrent().Details;
        if (_account?.VidPer(user.UserId, DateTime.UtcNow) is int vidEvento
            && dettagli.FirstOrDefault(d => d.UserId == vidEvento) is { } evento)
            return evento.Callsign;
        return dettagli.FirstOrDefault(d => d.UserId == user.UserId)?.Callsign;
    }

    public async Task<LiveViewResult> BuildAsync(string callsign, CancellationToken ct = default)
    {
        callsign = (callsign ?? "").Trim().ToUpperInvariant();
        if (callsign.Length == 0) return LiveViewResult.NotFound(callsign);
        if (!PuoVedere(callsign)) return LiveViewResult.NotAllowed(callsign);

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
        // L'ENTE di cui la postazione è una posizione (S49): decide il documento, qualunque sia il tipo della
        // posizione — a Pratica di Mare la torre fa l'avvicinamento, e chi è su LIRE_TWR apre la vIPI dell'ente.
        var ente = _enti is null ? null : await _enti.FindAsync(callsign, ct);
        var ctx = new LiveStationContext(callsign, sector, acc, structure, topology, snapshot.Callsigns,
            ente is { Mode: AtcUnitMode.OwnDocument } ? ente.Code : null,
            ente is { Mode: AtcUnitMode.InAccVipi },
            ente?.Id, ente?.AccCode, ente?.GroupKey);

        var kind = _registry.For(ctx);
        if (kind is null) return LiveViewResult.NotFound(callsign);

        return new LiveViewResult(await kind.BuildAsync(ctx, ct), callsign);
    }

    public async Task<IReadOnlyList<LiveStationOption>> ListStationsAsync(CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.DivisionStaff);

        // Riusa la query del picker di «Nuovo documento»: settori ATTIVI, ordinati per callsign. Un elenco
        // di postazioni selezionabili esiste già — qui cambia solo chi lo può chiedere.
        var online = _online.GetCurrent().Callsigns;
        var righe = await _sectors.ListSectorNodesAsync(ct);
        return righe
            .Select(s => new LiveStationOption(s.Callsign, s.AccCode, online.Contains(s.Callsign)))
            .ToList();
    }

    /// <summary>
    /// La regola, in una riga: <b>la vista live è la tua postazione</b>. Da DivisionStaff in su si può
    /// guardare quella di chiunque — per assistenza e supervisione, deciso dal committente il 5 settembre
    /// 2026 (carta <c>docs/feature/2026-09-05-vista-live-selettore-e-cancello.md</c>).
    ///
    /// <para>⚠️ È «la mia», non «non è aperta da altri»: un cancello che guardasse chi è online
    /// cambierebbe risposta a ogni tick del poller, e butterebbe fuori chi sta guardando nel momento in
    /// cui quell'ente apre.</para>
    /// </summary>
    private bool PuoVedere(string callsign) =>
        _authz.IsDivisionStaff || string.Equals(callsign, MyCallsign(), StringComparison.OrdinalIgnoreCase);
}
