using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Cache condivisa (singleton) dell'ATC online: una sola fotografia in memoria letta da tutti i client.
/// Aggiornata dal <c>AtcPollingHostedService</c> (~60s), notifica i sottoscrittori via <see cref="Changed"/>.
/// Thread-safe: pubblicazione atomica del riferimento immutabile (Volatile). ADR-0001 D6.
/// </summary>
public sealed class OnlineAtcCache : IOnlineAtcProvider
{
    private OnlineAtcSnapshot _current = OnlineAtcSnapshot.Empty;

    /// <summary>Sollevato dopo ogni aggiornamento della cache (alimenta il transport SSE / i refresh UI).</summary>
    public event Action? Changed;

    public OnlineAtcSnapshot GetCurrent() => Volatile.Read(ref _current);

    /// <summary>
    /// Pubblica una nuova fotografia e notifica. Chiamato solo dal poller.
    ///
    /// <para>🔴 <b>Ogni sottoscrittore per conto suo</b> (T-072, revisione del 13 settembre 2026). Con
    /// <c>Changed?.Invoke()</c> il primo che solleva interrompe gli altri e l'eccezione risale nel poller, che
    /// salta la registrazione delle sessioni e del traffico di quel minuto. Il caso vero: uno stream SSE che si
    /// chiude fra la copia dell'elenco dei sottoscrittori e la chiamata, e il cui semaforo è già smaltito.
    /// Chi guarda la cache non può rompere chi la scrive.</para>
    /// </summary>
    public void Set(OnlineAtcSnapshot snapshot)
    {
        Volatile.Write(ref _current, snapshot);
        if (Changed is not { } sottoscrittori) return;
        foreach (var s in sottoscrittori.GetInvocationList())
        {
            try { ((Action)s)(); }
            catch (Exception) { /* un sottoscrittore rotto non ferma gli altri, né il poller */ }
        }
    }
}
