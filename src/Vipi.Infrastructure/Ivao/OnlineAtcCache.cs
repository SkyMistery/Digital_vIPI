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

    /// <summary>Pubblica una nuova fotografia e notifica. Chiamato solo dal poller.</summary>
    public void Set(OnlineAtcSnapshot snapshot)
    {
        Volatile.Write(ref _current, snapshot);
        Changed?.Invoke();
    }
}
