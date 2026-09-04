using Vipi.Application.Abstractions;

namespace Vipi.Application.Translation;

/// <summary>
/// Com'è andato <b>un</b> giro di riempimento, e quando.
/// </summary>
/// <param name="QuandoUtc">Quando è finito.</param>
/// <param name="Da">Verso: la lingua di partenza.</param>
/// <param name="A">Verso: la lingua d'arrivo.</param>
/// <param name="Manuale">Vero se l'ha chiesto una persona col tasto «traduci ora», falso se è il giro
/// automatico. ⚠️ Le due cose si sommano nella spesa e si distinguono a schermo: «non è successo niente da
/// un'ora» e «l'ultima cosa successa l'ho chiesta io» sono due letture opposte della stessa riga.</param>
/// <param name="Rapporto">Che cosa ha fatto: aggiunte, già in memoria, rotte, chi ha risposto.</param>
public sealed record EsitoDelGiro(
    DateTime QuandoUtc, string Da, string A, bool Manuale, TranslationFillReport Rapporto);

/// <summary>
/// Quanto manca prima che le frasi appena scritte diventino leggibili nell'altra lingua (carta
/// <c>docs/feature/2026-09-04-stato-traduzione.md</c> §4-bis).
///
/// <para>
/// ⚠️ <b>È un orologio, non una stima</b>, e la differenza è tutta qui: il giro <b>non ha un tetto di
/// lotto</b> — <see cref="TranslationFillUseCase.EseguiSuAsync"/> spedisce in una chiamata tutti i segmenti
/// mancanti — quindi il numero di frasi appena scritte non allunga l'attesa. Quel che resta da sapere è
/// soltanto quando è passato l'ultimo giro, e quello è un fatto scritto nel database.
/// </para>
/// </summary>
/// <param name="UltimoUtc">L'ultimo giro <b>riuscito</b>, dal registro degli import
/// (<see cref="ImportCategories.Translation"/>). null = non ha mai girato in questa installazione.</param>
/// <param name="InCorso">Un giro sta girando adesso.</param>
/// <param name="Ultimi">Com'è andata le ultime volte, da quando l'host è acceso.</param>
public sealed record AttesaDelGiro(DateTime? UltimoUtc, bool InCorso, IReadOnlyList<EsitoDelGiro> Ultimi)
{
    /// <summary>Non si sa niente: non ha mai girato e non sta girando.</summary>
    public static readonly AttesaDelGiro Ignota = new(null, false, Array.Empty<EsitoDelGiro>());

    /// <inheritdoc cref="GiroDiTraduzione.Periodo"/>
    public TimeSpan Periodo => GiroDiTraduzione.Periodo;

    /// <summary>Quando passerà il prossimo giro. null se non ne è mai passato nessuno.</summary>
    public DateTime? ProssimoUtc => UltimoUtc is { } u ? u + GiroDiTraduzione.Periodo : null;

    /// <summary>
    /// Quanto manca al prossimo giro, mai negativo. null se non si sa.
    ///
    /// <para>⚠️ Zero è una risposta vera e va detta come «da un momento all'altro», non come «adesso»: il
    /// giro parte al suo scadere, non nell'istante in cui questa pagina si disegna.</para>
    /// </summary>
    public TimeSpan? Mancano(DateTime nowUtc) =>
        ProssimoUtc is { } p ? (p > nowUtc ? p - nowUtc : TimeSpan.Zero) : null;
}

/// <summary>Il giro che riempie la memoria di traduzione: le costanti che riguardano <b>tutti</b>.</summary>
public static class GiroDiTraduzione
{
    /// <summary>
    /// Ogni quanto passa il giro automatico.
    ///
    /// <para>Un quarto d'ora e non un giorno come gli altri import: qui il tempo che conta non è il ritmo di
    /// una sorgente ma <b>quanto aspetta un lettore</b> prima di vedere in inglese una frase appena scritta.</para>
    ///
    /// <para>⚠️ Sta qui e non nel servizio che lo esegue perché non lo guarda solo lui: l'editor dice a chi
    /// ha appena scritto <i>quanto manca</i>, e due costanti da un quarto d'ora in due file sono il modo in
    /// cui un giorno la pagina promette sei minuti e il giro ne impiega trenta.</para>
    /// </summary>
    public static readonly TimeSpan Periodo = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Chi ha girato per ultimo, e chi sta girando adesso.
///
/// <para>
/// 🔴 <b>Il lucchetto non è un dettaglio.</b> Il giro automatico e il tasto «traduci ora» spedirebbero gli
/// <b>stessi</b> segmenti se capitassero insieme — la memoria si legge all'inizio e si scrive alla fine — e
/// quei caratteri si pagherebbero due volte. Chi trova occupato non aspetta: torna indietro e lo dice, che è
/// la risposta giusta per un gesto che sta aspettando una persona davanti allo schermo.
/// </para>
///
/// <para>
/// ⚠️ <b>È in-processo, e basta così.</b> Il giro automatico e le pagine vivono nello stesso host: un
/// lucchetto sul database sarebbe una tabella in più per proteggere da uno scenario — due istanze — che
/// questo prodotto non ha. Il giorno che le istanze fossero due, il peggio che capita è una spesa doppia su
/// una pressione, non un dato sbagliato.
/// </para>
/// </summary>
public interface IRegistroDeiGiri
{
    /// <summary>Com'è andata le ultime volte, dalla più recente. Vuoto finché l'host non ne ha visto nessuno.</summary>
    IReadOnlyList<EsitoDelGiro> Ultimi { get; }

    /// <summary>Un giro sta girando adesso.</summary>
    bool InCorso { get; }

    /// <summary>Registra com'è andato un giro.</summary>
    void Registra(EsitoDelGiro esito);

    /// <summary>
    /// Prende il lucchetto, o torna <c>null</c> se qualcun altro sta già girando. Chi lo prende <b>deve</b>
    /// rilasciarlo (<c>using</c>).
    /// </summary>
    IDisposable? ProvaAEntrare();
}

/// <inheritdoc cref="IRegistroDeiGiri"/>
public sealed class RegistroDeiGiri : IRegistroDeiGiri
{
    /// <summary>Quanti esiti si tengono. Cinque: due versi per due giri, più uno.</summary>
    private const int Quanti = 5;

    private readonly object _serratura = new();
    private readonly LinkedList<EsitoDelGiro> _ultimi = new();
    private int _dentro;

    public IReadOnlyList<EsitoDelGiro> Ultimi
    {
        get { lock (_serratura) return _ultimi.ToList(); }
    }

    public bool InCorso => Volatile.Read(ref _dentro) != 0;

    public void Registra(EsitoDelGiro esito)
    {
        lock (_serratura)
        {
            _ultimi.AddFirst(esito);
            while (_ultimi.Count > Quanti) _ultimi.RemoveLast();
        }
    }

    public IDisposable? ProvaAEntrare() =>
        // ⚠️ `Interlocked` e non un `lock`: chi trova occupato deve tornare indietro SUBITO, non mettersi in
        // fila. Una pressione che aspetta un giro automatico appena partito resterebbe appesa per tutta la
        // durata di una chiamata di rete, e chi l'ha premuta crederebbe che il tasto sia rotto.
        Interlocked.CompareExchange(ref _dentro, 1, 0) == 0 ? new Uscita(this) : null;

    private sealed class Uscita : IDisposable
    {
        private readonly RegistroDeiGiri _registro;
        private int _fatto;
        public Uscita(RegistroDeiGiri registro) => _registro = registro;

        public void Dispose()
        {
            // Idempotente: un `using` annidato per sbaglio non deve aprire il lucchetto a metà giro.
            if (Interlocked.Exchange(ref _fatto, 1) == 0) Volatile.Write(ref _registro._dentro, 0);
        }
    }
}

/// <summary>Quanto manca al prossimo giro, per chi lo deve dire a schermo.</summary>
public interface IAttesaTraduzione
{
    /// <inheritdoc cref="AttesaDelGiro"/>
    Task<AttesaDelGiro> AttesaAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IAttesaTraduzione"/>
public sealed class AttesaTraduzione : IAttesaTraduzione
{
    private readonly IImportStateStore _stato;
    private readonly IRegistroDeiGiri _registro;

    public AttesaTraduzione(IImportStateStore stato, IRegistroDeiGiri registro)
    {
        _stato = stato;
        _registro = registro;
    }

    public async Task<AttesaDelGiro> AttesaAsync(CancellationToken ct = default)
    {
        // ⚠️ L'ultimo giro si chiede al DATABASE e non al registro in memoria: quello si svuota a ogni
        // riavvio dell'host, e dopo un riavvio direbbe «non ha mai girato» di una traduzione che gira da
        // settimane. Il registro serve al DETTAGLIO (com'è andata), il database al FATTO (quando).
        var ultimo = await _stato.GetLastSuccessAsync(ImportCategories.Translation, ct).ConfigureAwait(false);
        return new AttesaDelGiro(ultimo, _registro.InCorso, _registro.Ultimi);
    }
}
