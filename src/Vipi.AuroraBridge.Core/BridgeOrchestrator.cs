using Vipi.AuroraBridge.Contracts;

namespace Vipi.AuroraBridge.Core;

/// <summary>Cadenze del polling. Aurora non spinge nulla (nessun evento sul cambio di selezione), quindi
/// l'unico modo di accorgersi di una selezione nuova è chiedere.</summary>
public sealed record BridgePollingOptions(
    int SelectionMs = 1000,
    int RunwaysMs = 60000,
    int ConnectionMs = 30000);

/// <summary>Fotografia di ciò che il tool sa in questo momento: la mostra la UI, non la decide.</summary>
public sealed class BridgeState
{
    public bool AuroraConnected { get; init; }

    /// <summary>Postazione di cui si applicano le regole di trasferimento: quella connessa, o l'override.</summary>
    public string? OwnerCallsign { get; init; }

    /// <summary>Callsign REALMENTE connesso in Aurora (<c>#CONN</c>). Diverso da <see cref="OwnerCallsign"/>
    /// solo quando c'è un override, ed è questo che conta per sapere chi può scrivere.</summary>
    public string? ConnectedCallsign { get; init; }

    public string? SelectedTraffic { get; init; }

    /// <summary>Vero se il traffico selezionato è assunto dalla postazione connessa: solo allora Aurora
    /// accetta la scrittura dell'etichetta. Si confronta col callsign CONNESSO, non con l'override: l'override
    /// cambia le regole da applicare, non chi è al comando del traffico.</summary>
    public bool TrafficAssumed { get; init; }

    public FlightPlanRecord? FlightPlan { get; init; }
    public TrafficPositionRecord? Position { get; init; }
    public TransferResolveResponse? Proposal { get; init; }
    public bool ProposalFromCache { get; init; }
    public string? Notice { get; init; }

    /// <summary>Il candidato migliore, se c'è e se è scrivibile.</summary>
    public TransferCandidate? Best =>
        Proposal?.Candidates.FirstOrDefault(c => c.Writable) ?? Proposal?.Candidates.FirstOrDefault();
}

/// <summary>
/// Tiene insieme Aurora e il sito: sorveglia la selezione, raccoglie il contesto, chiede i candidati e li
/// pubblica. **Non scrive mai da solo**: la scrittura passa da <see cref="WriteAsync"/>, che la UI chiama
/// solo su azione esplicita dell'utente (decisione di piano, §1).
/// </summary>
public sealed class BridgeOrchestrator
{
    private readonly AuroraSession _aurora;
    private readonly VipiApiClient _api;
    private readonly BridgePollingOptions _polling;

    private string? _connected;
    private DateTime _connectionCheckedUtc = DateTime.MinValue;
    private IReadOnlyList<RunwayConfiguration> _runways = Array.Empty<RunwayConfiguration>();
    private DateTime _runwaysCheckedUtc = DateTime.MinValue;
    private string? _lastTraffic;

    private readonly string? _ownerOverride;

    /// <param name="ownerOverride">Postazione da usare al posto di quella riportata da <c>#CONN</c>. Serve quando
    /// il callsign connesso non è un settore del sito (sessioni di addestramento, callsign fuori standard) e in
    /// diagnostica: senza, il tool non avrebbe nulla da proporre e non si capirebbe perché.</param>
    public BridgeOrchestrator(AuroraSession aurora, VipiApiClient api, BridgePollingOptions? polling = null,
        string? ownerOverride = null)
    {
        _aurora = aurora;
        _api = api;
        _polling = polling ?? new BridgePollingOptions();
        _ownerOverride = string.IsNullOrWhiteSpace(ownerOverride) ? null : ownerOverride.Trim();
    }

    /// <summary>Emesso a ogni nuovo stato (selezione cambiata, proposta aggiornata, Aurora caduta).</summary>
    public event Action<BridgeState>? StateChanged;

    public BridgeState Current { get; private set; } = new();

    /// <summary>Un giro completo: selezione → contesto → proposta. Ritorna lo stato nuovo.
    /// Separato da <see cref="RunAsync"/> perché così è pilotabile a mano (CLI, test) senza avviare un ciclo.</summary>
    public async Task<BridgeState> RefreshAsync(bool force = false, CancellationToken ct = default)
    {
        var connected = await ConnectedAsync(ct).ConfigureAwait(false);
        var owner = connected is null ? null : _ownerOverride ?? connected;
        if (owner is null)
        {
            return Publish(new BridgeState
            {
                AuroraConnected = _aurora.IsConnected,
                Notice = _aurora.IsConnected
                    ? "Aurora non è connessa alla rete."
                    : "Aurora non raggiungibile: aprila e verifica F7 → Other → 3rd Party Software Access = YES.",
            });
        }

        var traffic = await _aurora.GetSelectedTrafficAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(traffic))
        {
            _lastTraffic = null;
            return Publish(new BridgeState
            {
                AuroraConnected = true,
                OwnerCallsign = owner,
                ConnectedCallsign = connected,
                Notice = "Nessun traffico selezionato in Aurora.",
            });
        }

        // Stessa selezione e nessuna richiesta esplicita: non ripeto il giro (né verso Aurora né verso il sito).
        if (!force && string.Equals(traffic, _lastTraffic, StringComparison.OrdinalIgnoreCase))
            return Current;

        _lastTraffic = traffic;

        var position = await _aurora.GetPositionAsync(traffic!, ct).ConfigureAwait(false);
        var plan = await _aurora.GetFlightPlanAsync(traffic!, ct).ConfigureAwait(false);
        var path = await _aurora.GetRoutePathAsync(traffic!, ct).ConfigureAwait(false);
        var runways = await RunwaysAsync(ct).ConfigureAwait(false);

        var snapshot = new FlightSnapshot(traffic!, owner, plan, position, path, runways);
        var outcome = await _api.ResolveAsync(FlightContextBuilder.Build(snapshot), ct).ConfigureAwait(false);

        return Publish(new BridgeState
        {
            AuroraConnected = true,
            OwnerCallsign = owner,
            ConnectedCallsign = connected,
            SelectedTraffic = traffic,
            // Chi può scrivere lo decide Aurora sulla CONNESSIONE, non sull'override delle regole.
            TrafficAssumed = position?.IsAssumedBy(connected) ?? false,
            FlightPlan = plan,
            Position = position,
            Proposal = outcome.Response,
            ProposalFromCache = outcome.FromCache,
            Notice = outcome.Error,
        });
    }

    /// <summary>Ciclo di sorveglianza. Si ferma solo col token: gli errori di rete o di Aurora finiscono
    /// nello stato come avviso, non interrompono il ciclo (in sessione il tool non deve morire).</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(force: false, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Publish(new BridgeState { AuroraConnected = _aurora.IsConnected, Notice = ex.Message });
            }

            try
            {
                await Task.Delay(_polling.SelectionMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>
    /// Scrive il livello di un candidato nell'etichetta quota. È l'UNICO punto in cui il tool modifica Aurora,
    /// e va chiamato solo da un'azione dell'utente.
    ///
    /// Non rilegge il tag per verificare: dopo <c>#LBALT</c> il record posizione resta stale per un paio di
    /// secondi (F0 §11.1), quindi una verifica immediata mentirebbe. Il valore nuovo si vede al giro dopo.
    /// </summary>
    public async Task<WriteResult> WriteAsync(TransferCandidate candidate, CancellationToken ct = default)
    {
        var state = Current;
        if (string.IsNullOrWhiteSpace(state.SelectedTraffic))
            return WriteResult.Fail("Nessun traffico selezionato.");
        if (!candidate.Writable || string.IsNullOrWhiteSpace(candidate.AuroraValue))
            return WriteResult.Fail("Questo livello non è scrivibile come etichetta.");
        if (!state.TrafficAssumed)
            return WriteResult.Fail($"{state.SelectedTraffic} non è assunto: Aurora rifiuterebbe la scrittura.");

        return await _aurora.SetAltitudeLabelAsync(state.SelectedTraffic!, candidate.AuroraValue, ct).ConfigureAwait(false);
    }

    /// <summary>Cancella l'etichetta quota del traffico selezionato.</summary>
    public Task<WriteResult> ClearAsync(CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(Current.SelectedTraffic)
            ? Task.FromResult(WriteResult.Fail("Nessun traffico selezionato."))
            : _aurora.ClearAltitudeLabelAsync(Current.SelectedTraffic!, ct);

    /// <summary>Callsign connesso, con cache breve. Interrogato SEMPRE, anche con l'override attivo: se Aurora
    /// non risponde a <c>#CONN</c> la connessione è caduta, e proseguire fingerebbe una postazione che non c'è.</summary>
    private async Task<string?> ConnectedAsync(CancellationToken ct)
    {
        if (_connected is not null && (DateTime.UtcNow - _connectionCheckedUtc).TotalMilliseconds < _polling.ConnectionMs)
            return _connected;

        _connected = await _aurora.GetConnectedCallsignAsync(ct).ConfigureAwait(false);
        _connectionCheckedUtc = DateTime.UtcNow;
        return _connected;
    }

    private async Task<IReadOnlyList<RunwayConfiguration>> RunwaysAsync(CancellationToken ct)
    {
        if ((DateTime.UtcNow - _runwaysCheckedUtc).TotalMilliseconds < _polling.RunwaysMs)
            return _runways;

        _runways = await _aurora.GetControlledRunwaysAsync(ct).ConfigureAwait(false);
        _runwaysCheckedUtc = DateTime.UtcNow;
        return _runways;
    }

    private BridgeState Publish(BridgeState state)
    {
        Current = state;
        StateChanged?.Invoke(state);
        return state;
    }
}
