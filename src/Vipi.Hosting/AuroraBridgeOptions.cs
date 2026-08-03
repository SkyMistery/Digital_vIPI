using System.Collections.Concurrent;
using Vipi.Application.Content;

namespace Vipi.Hosting;

/// <summary>
/// Configurazione dell'API del bridge Aurora (sezione «AuroraBridge»). Tutti i valori hanno un default
/// sensato: senza sezione nel file l'endpoint funziona comunque.
/// </summary>
public sealed class AuroraBridgeOptions
{
    public const string SectionName = "AuroraBridge";

    /// <summary>Convenzione della stringa scritta nell'etichetta quota di Aurora: «Number» (default) o «FlPrefixed».
    /// È una scelta di leggibilità del tag: Aurora accetta testo libero (piano §11.2).</summary>
    public string? LabelConvention { get; set; }

    /// <summary>Quanti candidati restituire al massimo.</summary>
    public int MaxCandidates { get; set; } = 8;

    /// <summary>Richieste al minuto ammesse per IP. L'endpoint è anonimo: senza tetto, un client difettoso
    /// in polling stretto basterebbe a caricare il DB del sito.</summary>
    public int RequestsPerMinutePerIp { get; set; } = 120;

    /// <summary>Tetto del corpo della richiesta. Le rotte sono lunghe, ma non megabyte.</summary>
    public int MaxRequestBytes { get; set; } = 64 * 1024;

    /// <summary>Traduce le opzioni di configurazione nei parametri del matcher puro.</summary>
    public TransferMatchOptions ToMatchOptions() => new(
        Enum.TryParse<AuroraLabelConvention>(LabelConvention, ignoreCase: true, out var c) ? c : AuroraLabelConvention.Number,
        MaxCandidates <= 0 ? 8 : MaxCandidates);
}

/// <summary>
/// Limitatore a finestra fissa: al massimo N richieste al minuto per chiave (l'IP del chiamante).
/// Distinto da <see cref="StaffLoginThrottle"/>, che invece consente UNA azione per finestra ed è pensato
/// per non ripetere una scrittura: qui serve un CONTEGGIO, non un «già fatto».
/// </summary>
public sealed class RequestRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, Counter> _counters = new();

    private sealed class Counter
    {
        public DateTime WindowStart;
        public int Count;
    }

    /// <summary>True se la richiesta può passare. Thread-safe: il conteggio della finestra è sotto lock del
    /// singolo contatore, così due richieste concorrenti dello stesso IP non si sovrascrivono a vicenda.</summary>
    public bool TryAcquire(string key, int limitPerMinute)
    {
        if (limitPerMinute <= 0) return true;

        var counter = _counters.GetOrAdd(key, _ => new Counter { WindowStart = DateTime.UtcNow });
        lock (counter)
        {
            var now = DateTime.UtcNow;
            if (now - counter.WindowStart >= Window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }
            if (counter.Count >= limitPerMinute) return false;
            counter.Count++;
            return true;
        }
    }
}
