using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Vipi.AuroraBridge.Contracts;

namespace Vipi.AuroraBridge.Core;

/// <summary>
/// Riga di candidato pronta per la vista: testi già composti e — soprattutto — il MOTIVO per cui la
/// scrittura è o non è possibile. La UI non deve decidere nulla, solo mostrare.
/// </summary>
public sealed class CandidateRow
{
    public required TransferCandidate Candidate { get; init; }
    public required string Cop { get; init; }
    public required string Level { get; init; }
    public required string Handler { get; init; }
    public required string Reasons { get; init; }
    public required string ConditionBadge { get; init; }
    public required bool CanWrite { get; init; }

    /// <summary>Testo del pulsante di scrittura, oppure la ragione dell'impedimento. Non esiste un pulsante
    /// grigio senza spiegazione: in sessione, «non funziona» senza perché è peggio di niente.</summary>
    public required string WriteHint { get; init; }

    public string HandlerNote => Candidate.HandlerOnline ? "" : " (offline)";
}

/// <summary>
/// Stato della finestra. Vive in Core e non conosce Avalonia: così la logica di presentazione — quando il
/// pulsante si accende, cosa c'è scritto sopra, come si formatta una riga — è coperta da test normali,
/// mentre il progetto UI resta XAML e binding.
/// </summary>
public sealed class BridgeViewModel : INotifyPropertyChanged
{
    private readonly BridgeOrchestrator _orchestrator;

    private string _status = "Avvio…";
    private string _flightLine = "";
    private string _ownerLine = "";
    private string? _warning;
    private bool _busy;

    public BridgeViewModel(BridgeOrchestrator orchestrator, BridgeSettings settings, BridgeLog? log = null)
    {
        _orchestrator = orchestrator;
        Settings = settings;
        Log = log ?? new BridgeLog();
        _orchestrator.StateChanged += Apply;
    }

    public BridgeSettings Settings { get; }

    public BridgeLog Log { get; }

    public ObservableCollection<CandidateRow> Candidates { get; } = new();

    public string Status { get => _status; private set => Set(ref _status, value); }
    public string FlightLine { get => _flightLine; private set => Set(ref _flightLine, value); }
    public string OwnerLine { get => _ownerLine; private set => Set(ref _ownerLine, value); }
    public string? Warning { get => _warning; private set => Set(ref _warning, value); }
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);
    public bool Busy { get => _busy; private set => Set(ref _busy, value); }

    /// <summary>Marshalling verso il thread della UI. Lo inietta il progetto Avalonia (Dispatcher.UIThread.Post);
    /// nei test resta l'esecuzione diretta.</summary>
    public Action<Action> Post { get; set; } = action => action();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Ricalcola tutto lo stato mostrato a partire da una fotografia dell'orchestratore.</summary>
    public void Apply(BridgeState state) => Post(() =>
    {
        Status = state.AuroraConnected ? "Aurora connessa" : "Aurora non raggiungibile";

        OwnerLine = state.OwnerCallsign is null
            ? ""
            : state.ConnectedCallsign is not null && !string.Equals(state.ConnectedCallsign, state.OwnerCallsign, StringComparison.OrdinalIgnoreCase)
                ? $"{state.OwnerCallsign}  (connesso come {state.ConnectedCallsign})"
                : state.OwnerCallsign;

        FlightLine = FormatFlight(state);
        Warning = ComposeWarning(state);

        Candidates.Clear();
        foreach (var row in BuildRows(state)) Candidates.Add(row);
    });

    /// <summary>Mostra un messaggio nella barra avvisi (esiti della scorciatoia, problemi di avvio).</summary>
    public void Notify(string message) => Post(() => Warning = message);

    /// <summary>Un giro forzato (pulsante «Aggiorna»).</summary>
    public async Task RefreshAsync()
    {
        Busy = true;
        try { await _orchestrator.RefreshAsync(force: true).ConfigureAwait(false); }
        finally { Post(() => Busy = false); }
    }

    /// <summary>Scrive il candidato scelto. È l'unica via verso Aurora, e parte solo da un gesto dell'utente.</summary>
    public async Task<string> WriteAsync(CandidateRow row)
    {
        var traffic = _orchestrator.Current.SelectedTraffic;
        var result = await _orchestrator.WriteAsync(row.Candidate).ConfigureAwait(false);
        Log.WroteLabel(traffic, row.Candidate.AuroraValue, row.Candidate.Cop, result.Ok, result.Error);

        var message = result.Ok
            ? $"Scritto «{row.Candidate.AuroraValue}» su {traffic}. Il tag si aggiorna al prossimo giro radar."
            : $"Rifiutata: {result.Error}";

        Post(() => Warning = message);
        return message;
    }

    /// <summary>
    /// Scrive il candidato migliore: è ciò che fa la combinazione globale, per non staccare le mani dalla PVD.
    /// Sceglie solo fra i candidati **scrivibili** e solo se la prima riga è già quella: se il migliore in
    /// graduatoria non è scrivibile, la scorciatoia non «ripiega» su un altro livello di nascosto — chiede
    /// all'utente di guardare la finestra.
    /// </summary>
    public async Task<string> WriteBestAsync()
    {
        var first = Candidates.FirstOrDefault();
        if (first is null)
        {
            var empty = "Nessun candidato: niente da scrivere.";
            Post(() => Warning = empty);
            return empty;
        }

        if (!first.CanWrite)
        {
            var blocked = $"Non scrivo: {first.WriteHint}.";
            Post(() => Warning = blocked);
            return blocked;
        }

        return await WriteAsync(first).ConfigureAwait(false);
    }

    public async Task<string> ClearAsync()
    {
        var traffic = _orchestrator.Current.SelectedTraffic;
        var result = await _orchestrator.ClearAsync().ConfigureAwait(false);
        Log.WroteLabel(traffic, "", "—", result.Ok, result.Error);

        var message = result.Ok ? "Etichetta cancellata." : $"Rifiutata: {result.Error}";
        Post(() => Warning = message);
        return message;
    }

    // --- composizione dei testi (pura: è ciò che i test verificano) ---

    internal static string FormatFlight(BridgeState s)
    {
        if (s.SelectedTraffic is null) return "Nessun traffico selezionato";

        var fp = s.FlightPlan;
        var route = fp is null ? "" : $"   {fp.Departure ?? "?"} → {fp.Arrival ?? "?"}";
        var cruise = fp?.CruiseFlightLevel is int fl ? $"   crociera FL{fl}" : "";
        var altitude = s.Position?.AltitudeFt is int ft ? $"   {ft:N0} ft" : "";
        var assumed = s.TrafficAssumed ? "   ASSUNTO" : "   non assunto";

        return $"{s.SelectedTraffic}{route}{cruise}{altitude}{assumed}";
    }

    internal static string? ComposeWarning(BridgeState s)
    {
        var parts = new List<string>();
        if (s.Notice is not null) parts.Add(s.Notice);
        if (s.ProposalFromCache) parts.Add("Proposta dalla cache locale: il sito non ha risposto.");
        if (s.Proposal is not null) parts.AddRange(s.Proposal.Warnings);
        return parts.Count == 0 ? null : string.Join("  ·  ", parts);
    }

    internal static IEnumerable<CandidateRow> BuildRows(BridgeState state)
    {
        var candidates = state.Proposal?.Candidates ?? new List<TransferCandidate>();
        foreach (var c in candidates)
        {
            var (canWrite, hint) = WriteAbility(c, state.TrafficAssumed, state.SelectedTraffic);
            yield return new CandidateRow
            {
                Candidate = c,
                Cop = string.IsNullOrWhiteSpace(c.Cop) ? "—" : c.Cop,
                Level = string.IsNullOrWhiteSpace(c.Level.Text) ? "—" : c.Level.Text,
                Handler = c.ResolvedHandler ?? "—",
                Reasons = string.Join(" · ", c.Reasons),
                ConditionBadge = ConditionBadge(c.Condition),
                CanWrite = canWrite,
                WriteHint = hint,
            };
        }
    }

    /// <summary>Le tre ragioni per cui non si può scrivere, in ordine di precedenza. Ognuna ha un testo suo:
    /// «livello mancante nella vIPI» e «traffico non assunto» sono problemi diversi e si risolvono diversamente.</summary>
    internal static (bool CanWrite, string Hint) WriteAbility(TransferCandidate c, bool assumed, string? traffic)
    {
        if (traffic is null) return (false, "Nessun traffico selezionato");
        if (!c.Writable || string.IsNullOrWhiteSpace(c.AuroraValue))
            return (false, "Livello non scrivibile: manca il valore nella vIPI");
        if (!assumed) return (false, "Traffico non assunto: Aurora rifiuta la scrittura");

        return (true, $"Scrivi «{c.AuroraValue}»");
    }

    internal static string ConditionBadge(CandidateCondition condition) => condition.Match switch
    {
        "matched" => $"✓ {condition.Display}",
        "unmatched" => $"✗ {condition.Display}",
        "unknown" => $"? {condition.Display ?? "condizione non verificabile"}",
        _ => "",
    };

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name == nameof(Warning)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasWarning)));
    }
}
