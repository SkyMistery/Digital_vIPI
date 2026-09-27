using Microsoft.Extensions.Logging;

namespace Vipi.Host;

/// <summary>
/// Un tetto ai circuiti Blazor <b>anonimi</b> aperti insieme: oltre la soglia, un anonimo in più non si collega
/// e la pagina resta quella disegnata dal server, senza interattività.
///
/// <para>🔴 <b>Perché (U-237, revisione totale 3).</b> Ogni pagina interattiva tiene in memoria un circuito per
/// visitatore, e nessuno contava quanti. Uno script che apre migliaia di WebSocket su una pagina pubblica fa
/// crescere la memoria dell'unico processo finché l'host non lo spegne — e con lui il sito per tutti, staff
/// compreso. Il committente ha scelto il tetto nel codice (27 settembre 2026), non una regola al bordo.</para>
///
/// <para><b>Che cosa si conta.</b> Le connessioni di trasporto ancora aperte: la GET su <c>/_blazor</c> (il
/// WebSocket, o lo stream SSE, o l'attesa del long polling) resta in volo per tutta la vita della connessione,
/// quindi il contatore sale all'ingresso e scende quando la connessione si chiude. I circuiti staccati e
/// trattenuti non passano di qui: li limita già <c>DisconnectedCircuitMaxRetained</c> (25). Le POST del long
/// polling non si contano e non si rifiutano mai: sono i messaggi di una connessione già ammessa.</para>
///
/// <para>⚠️ <b>Un tetto solo, globale, e niente tetto per IP.</b> L'IP del chiamante qui arriva da
/// <c>X-Forwarded-For</c> dietro Cloudflare e nginx: o lo sceglie il chiamante (e ruotandolo si aggira il tetto),
/// o è quello del nodo di Cloudflare (e allora tanti soci veri sembrano una persona sola). È la stessa ragione per
/// cui il bridge Aurora ha anche un tetto complessivo (<c>AuroraBridgeOptions.RequestsPerMinuteTotal</c>). Il tetto
/// globale è l'unico che regge davvero: sotto attacco gli anonimi restano fuori, il processo resta vivo.</para>
///
/// <para>⚠️ <b>Chi è entrato col VID non si conta e non si ferma</b>: lo staff deve poter lavorare proprio quando
/// il sito è sotto pressione. In sviluppo, dove l'identità è finta e non passa dal <c>ClaimsPrincipal</c>, tutti
/// sono «anonimi»: col tetto di default non se ne accorge nessuno.</para>
///
/// <para>Chi resta fuori riceve un 503 con <c>Retry-After</c>. <c>vipi-riconnessione.js</c> scrive l'avvio fallito
/// in console e non ricarica: niente giro di ricariche che moltiplica il carico.</para>
/// </summary>
public sealed class TettoDeiCircuitiAnonimi
{
    /// <summary>
    /// ⚠️ Stimato, non misurato: il traffico atteso sono decine di persone, e la vIPI pubblicata il giorno AIRAC
    /// qualche centinaio di lettori che per lo più arrivano e se ne vanno. Duecento circuiti anonimi insieme sono
    /// molto sopra il giorno peggiore e molto sotto quel che serve a un attacco. Si cambia con
    /// <c>Circuiti:TettoAnonimi</c>; zero o meno = nessun tetto.
    /// </summary>
    public const int TettoPredefinito = 200;

    /// <summary>La chiave di configurazione del tetto.</summary>
    public const string ChiaveConfigurazione = "Circuiti:TettoAnonimi";

    private const string Percorso = "/_blazor";
    private const string Negoziazione = "/_blazor/negotiate";

    /// <summary>Quanto aspettare prima di riprovare, detto a chi resta fuori.</summary>
    private const int SecondiPrimaDiRiprovare = 30;

    private readonly int _tetto;
    private readonly ILogger? _registro;
    private int _aperti;
    private long _ultimoAvviso;
    private long _rifiutati;

    public TettoDeiCircuitiAnonimi(int tetto, ILogger? registro = null)
    {
        _tetto = tetto;
        _registro = registro;
    }

    /// <summary>Connessioni anonime aperte adesso.</summary>
    public int Aperti => Volatile.Read(ref _aperti);

    /// <summary>Quante richieste sono state lasciate fuori da quando il processo è partito.</summary>
    public long Rifiutati => Interlocked.Read(ref _rifiutati);

    public async Task PassaAsync(HttpContext context, RequestDelegate next)
    {
        if (_tetto <= 0 || context.User?.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        var percorso = context.Request.Path;

        // La stretta di mano: non apre niente, ma se la sala è piena è inutile cominciare.
        if (percorso.Equals(Negoziazione, StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(context.Request.Method))
        {
            if (Aperti >= _tetto) { Rifiuta(context); return; }
            await next(context);
            return;
        }

        // Il trasporto: la GET che resta aperta finché vive la connessione. Solo lei si conta.
        if (!percorso.Equals(Percorso, StringComparison.OrdinalIgnoreCase) || !HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        // ⚠️ Prima si prende il posto, poi si guarda: controllare e poi incrementare lascerebbe passare tutti
        // quelli che arrivano nello stesso istante.
        if (Interlocked.Increment(ref _aperti) > _tetto)
        {
            Interlocked.Decrement(ref _aperti);
            Rifiuta(context);
            return;
        }

        try { await next(context); }
        finally { Interlocked.Decrement(ref _aperti); }
    }

    private void Rifiuta(HttpContext context)
    {
        Interlocked.Increment(ref _rifiutati);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = SecondiPrimaDiRiprovare.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Una riga al minuto, non una per rifiuto: sotto attacco i rifiuti sono migliaia, e un registro che
        // cresce con loro è lo stesso esaurimento spostato sul disco.
        var ora = Environment.TickCount64;
        var prima = Interlocked.Read(ref _ultimoAvviso);
        if (ora - prima >= 60_000 && Interlocked.CompareExchange(ref _ultimoAvviso, ora, prima) == prima)
            _registro?.LogWarning(
                "Tetto dei circuiti anonimi raggiunto ({Tetto}): {Rifiutati} richieste lasciate fuori dall'avvio.",
                _tetto, Rifiutati);
    }
}
