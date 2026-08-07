using System.Collections.Concurrent;
using Vipi.Application.Content;

namespace Vipi.Hosting;

/// <summary>
/// Configurazione dell'API del bridge Aurora (sezione «AuroraBridge»). Tutti i valori hanno un default
/// sensato: senza sezione nel file il modulo parte comunque — ma l'endpoint <b>non</b> viene montato, vedi
/// <see cref="Enabled"/>.
/// </summary>
public sealed class AuroraBridgeOptions
{
    public const string SectionName = "AuroraBridge";

    /// <summary>
    /// Se montare l'endpoint <c>POST /vsop/api/v1/transfers/resolve</c>. <b>Default <c>false</c></b>: è
    /// superficie pubblica e anonima su un sito servito a una divisione, e accenderla è una decisione, non
    /// una conseguenza di aver fuso un ramo. Chi distribuisce il tool desktop la accende quando serve.
    ///
    /// <para>Spento, la rotta non esiste affatto (404) invece di rispondere 403: un endpoint che nega dice
    /// comunque di esserci.</para>
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Convenzione della stringa scritta nell'etichetta quota di Aurora: «Number» (default) o «FlPrefixed».
    /// È una scelta di leggibilità del tag: Aurora accetta testo libero (piano §11.2).</summary>
    public string? LabelConvention { get; set; }

    /// <summary>Quanti candidati restituire al massimo.</summary>
    public int MaxCandidates { get; set; } = 8;

    /// <summary>
    /// Richieste al minuto ammesse per IP. L'endpoint è anonimo: senza tetto, un client difettoso in polling
    /// stretto basterebbe a caricare il DB del sito.
    ///
    /// <para>⚠️ Da solo non basta, e va saputo: dietro il reverse proxy l'IP del chiamante arriva da
    /// <c>X-Forwarded-For</c> e <c>UseForwardedHeaders</c> è configurato senza proxy noti (l'indirizzo del
    /// proxy non è fisso). Quindi la chiave di questo tetto <b>la sceglie il chiamante</b> e ruotandola lo si
    /// aggira. È la ragione per cui esiste anche <see cref="RequestsPerMinuteTotal"/>.</para>
    /// </summary>
    public int RequestsPerMinutePerIp { get; set; } = 120;

    /// <summary>
    /// Tetto complessivo dell'endpoint, di tutti i chiamanti insieme. È l'unico che regge davvero contro un
    /// <c>X-Forwarded-For</c> che cambia a ogni richiesta. Zero o meno = nessun tetto complessivo.
    /// </summary>
    public int RequestsPerMinuteTotal { get; set; } = 600;

    /// <summary>
    /// Quanti IP distinti il limitatore tiene in memoria. Serve perché la chiave è spoofabile: senza un tetto
    /// qui, ruotare l'header basta a far crescere il dizionario senza limite — un esaurimento di memoria a
    /// colpi di richieste da 200 byte. Oltre il tetto, un IP mai visto viene rifiutato (429) invece che tracciato.
    /// </summary>
    public int MaxTrackedClients { get; set; } = 10_000;

    /// <summary>Tetto del corpo della richiesta. Le rotte sono lunghe, ma non megabyte.</summary>
    public int MaxRequestBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// Per quanti secondi riusare la topologia globale fra una richiesta e l'altra. Senza cache ogni chiamata
    /// rilegge tutti i settori attivi: su un database condiviso con il sito che ci ospita è il costo che si
    /// nota per primo. La gerarchia cambia per azione di un admin, non per richiesta. Zero = nessuna cache.
    /// </summary>
    public int TopologyCacheSeconds { get; set; } = 30;

    /// <summary>Traduce le opzioni di configurazione nei parametri del matcher puro.</summary>
    public TransferMatchOptions ToMatchOptions() => new(
        Enum.TryParse<AuroraLabelConvention>(LabelConvention, ignoreCase: true, out var c) ? c : AuroraLabelConvention.Number,
        MaxCandidates <= 0 ? 8 : MaxCandidates);

    /// <summary>Tetto del corpo effettivamente applicato: un valore non positivo ricade sul default invece di
    /// significare «illimitato», che su un endpoint anonimo non è mai ciò che si intendeva.</summary>
    public int EffectiveMaxRequestBytes => MaxRequestBytes > 0 ? MaxRequestBytes : 64 * 1024;

    /// <summary>Durata della cache di topologia, normalizzata.</summary>
    public TimeSpan TopologyCacheTtl => TimeSpan.FromSeconds(Math.Max(0, TopologyCacheSeconds));
}

/// <summary>
/// Limitatore a finestra fissa: al massimo N richieste al minuto per chiave (l'IP del chiamante, o la chiave
/// unica <see cref="GlobalKey"/> per il tetto complessivo).
/// Distinto da <see cref="StaffLoginThrottle"/>, che invece consente UNA azione per finestra ed è pensato
/// per non ripetere una scrittura: qui serve un CONTEGGIO, non un «già fatto».
/// </summary>
public sealed class RequestRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Chiave del tetto complessivo. Il carattere nullo non può comparire in un indirizzo IP, quindi
    /// nessun chiamante può spacciarsi per il contatore globale.</summary>
    public const string GlobalKey = "\0totale";

    private readonly ConcurrentDictionary<string, Counter> _counters = new();

    private sealed class Counter
    {
        public DateTime WindowStart;
        public int Count;
    }

    /// <summary>Quante chiavi il limitatore sta tenendo (diagnostica e test).</summary>
    public int TrackedKeys => _counters.Count;

    /// <summary>
    /// True se la richiesta può passare. Thread-safe: il conteggio della finestra è sotto lock del singolo
    /// contatore, così due richieste concorrenti della stessa chiave non si sovrascrivono a vicenda.
    ///
    /// <para><paramref name="maxTrackedKeys"/> limita quante chiavi distinte si tengono in memoria. Quando si
    /// arriva al tetto si spazzano prima le finestre scadute; se dopo la pulizia non c'è ancora posto, una
    /// chiave <b>nuova</b> viene rifiutata. È deliberatamente severo verso chi non abbiamo mai visto: quello
    /// stato lo si raggiunge solo con una chiave che cambia a ogni richiesta, che è l'attacco, non l'uso.</para>
    /// </summary>
    public bool TryAcquire(string key, int limitPerMinute, int maxTrackedKeys = 0)
    {
        if (limitPerMinute <= 0) return true;

        if (!_counters.TryGetValue(key, out var counter))
        {
            if (maxTrackedKeys > 0 && _counters.Count >= maxTrackedKeys)
            {
                SpazzaScadute();
                if (_counters.Count >= maxTrackedKeys) return false;
            }
            counter = _counters.GetOrAdd(key, _ => new Counter { WindowStart = DateTime.UtcNow });
        }

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

    /// <summary>Toglie i contatori la cui finestra è chiusa: sono chiavi che non contano più nulla.</summary>
    private void SpazzaScadute()
    {
        var now = DateTime.UtcNow;
        foreach (var (key, counter) in _counters)
        {
            if (key == GlobalKey) continue;   // il contatore complessivo non si spazza mai: è uno solo
            bool scaduta;
            lock (counter) scaduta = now - counter.WindowStart >= Window;
            if (scaduta) _counters.TryRemove(key, out _);
        }
    }
}
