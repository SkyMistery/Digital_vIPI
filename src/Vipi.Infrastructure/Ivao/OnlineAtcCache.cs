using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Cache condivisa (singleton) dell'ATC online: una sola fotografia in memoria letta da tutti i client.
/// Aggiornata dal <c>AtcPollingHostedService</c> (~60s), notifica i sottoscrittori via <see cref="Changed"/>.
/// Thread-safe: pubblicazione atomica del riferimento immutabile (Volatile). ADR-0001 D6.
///
/// <para>🔴 <b>La fotografia scade</b> (T-034, revisione del 13 settembre 2026). Se il poll fallisce il poller
/// tiene l'ultima fotografia, e senza scadenza il pallino «in frequenza», la presidenza degli aeroporti e i
/// punti di trasferimento mostravano per ore controllori che avevano staccato. Oltre la soglia la lettura torna
/// elenchi vuoti con <see cref="OnlineAtcSnapshot.Expired"/>: la regola sta nella porta, non in ogni pagina.</para>
/// </summary>
public sealed class OnlineAtcCache : IOnlineAtcProvider
{
    /// <summary>La soglia di chi non la dice: cinque minuti, la stessa che rende «Degraded» la salute.</summary>
    public static readonly TimeSpan ScadenzaPredefinita = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _orologio;
    private readonly TimeSpan _scadenza;

    private OnlineAtcSnapshot _current = OnlineAtcSnapshot.Empty;

    /// <summary>La vista scaduta dell'ultima fotografia, fatta una volta sola per fotografia: le liste la leggono
    /// una volta per riga. Un riferimento solo, così un lettore concorrente non vede mezza coppia.</summary>
    private sealed record Scaduta(OnlineAtcSnapshot Di, OnlineAtcSnapshot Vista);
    private Scaduta? _scaduta;

    public OnlineAtcCache(TimeProvider? orologio = null, TimeSpan? scadenza = null)
    {
        _orologio = orologio ?? TimeProvider.System;
        _scadenza = scadenza ?? ScadenzaPredefinita;
    }

    /// <summary>
    /// Soglia per un poller che gira ogni <paramref name="giro"/>: tre giri persi, mai meno di
    /// <see cref="ScadenzaPredefinita"/> — con il giro da un minuto un solo whazzup lento non deve spegnere tutti.
    /// </summary>
    public static TimeSpan ScadenzaPer(TimeSpan giro) =>
        giro * 3 > ScadenzaPredefinita ? giro * 3 : ScadenzaPredefinita;

    /// <summary>Sollevato dopo ogni aggiornamento della cache (alimenta il transport SSE / i refresh UI).</summary>
    public event Action? Changed;

    public OnlineAtcSnapshot GetCurrent()
    {
        var corrente = Volatile.Read(ref _current);
        if (corrente.AsOf == DateTimeOffset.MinValue || _orologio.GetUtcNow() - corrente.AsOf <= _scadenza)
            return corrente;

        if (Volatile.Read(ref _scaduta) is { } s && ReferenceEquals(s.Di, corrente)) return s.Vista;
        var vista = new OnlineAtcSnapshot
        {
            Callsigns = OnlineAtcSnapshot.Empty.Callsigns,
            Details = OnlineAtcSnapshot.Empty.Details,
            AsOf = corrente.AsOf,
            Expired = true,
        };
        Volatile.Write(ref _scaduta, new Scaduta(corrente, vista));
        return vista;
    }

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
