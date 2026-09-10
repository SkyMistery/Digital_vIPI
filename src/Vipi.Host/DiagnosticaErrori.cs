using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Vipi.Application.Diagnostica;

namespace Vipi.Host;

/// <summary>
/// Il registro delle richieste finite male, su file, accanto a <see cref="StartupDiagnostics"/> e per lo
/// stesso motivo: su <c>atc.it.ivao.aero</c> <b>i log del processo non li legge nessuno</b>. Niente shell,
/// niente pannello, solo FTP — e ASP.NET Core l'eccezione la scrive su <c>stdout</c>, che lì è il vuoto.
///
/// <para><b>Perché esiste.</b> Il 23 agosto 2026 un login rotto ha costretto a ricostruire la causa dagli
/// <c>scope</c> dentro il <c>code</c> OIDC, perché di quel guasto non era rimasta una riga; il 24 un socio
/// ha mandato la fotografia di una pagina «Error.» e di nuovo non c'era niente da leggere. Due volte in due
/// giorni la stessa mancanza. Il codice mostrato in pagina da <see cref="PaginaErrore"/> è lo stesso che si
/// trova qui: dalla fotografia si arriva allo stack trace.</para>
///
/// <para><b>Cosa NON entra nel file.</b> La stringa di query — su <c>/signin-oidc</c> porta il <c>code</c>
/// OAuth, che è una credenziale — i cookie, e le intestazioni. Restano metodo, percorso, VID e l'eccezione:
/// abbastanza per capire, abbastanza poco perché il file si possa spedire per email.</para>
///
/// <para>⚠️ Sta sotto <c>diagnostica/</c>, che <b>non</b> è dentro <c>wwwroot</c> e che il proxy nega
/// esplicitamente (<c>deploy/atc-ivao/nginx-vipi.conf</c>): uno stack trace scaricabile dal web sarebbe una
/// mappa del server regalata a chi passa.</para>
/// </summary>
public static class DiagnosticaErrori
{
    /// <summary>Le ultime richieste finite in eccezione, la più recente in fondo.</summary>
    public const string NomeFile = "errori-richieste.txt";

    /// <summary>Il giro precedente, conservato quando il file corrente supera <see cref="TettoByte"/>.</summary>
    public const string NomeFilePrecedente = "errori-richieste-precedenti.txt";

    /// <summary>
    /// Oltre questa soglia il file si mette da parte e se ne comincia uno nuovo. Due file e non dieci: chi
    /// li scarica via FTP li apre a mano, e la storia che serve è quella di oggi.
    /// </summary>
    private const long TettoByte = 512 * 1024;

    /// <summary>Le richieste che falliscono insieme sono richieste diverse: si scrive una alla volta.</summary>
    private static readonly object Serratura = new();

    /// <summary>
    /// Quanto può essere vecchia una fotografia delle collisioni perché valga ancora la pena di allegarla a
    /// QUESTO errore. Dieci secondi: una corsa sul <c>DbContext</c> e l'eccezione che ne esce distano
    /// millisecondi, non minuti — se la fotografia è più vecchia è di un altro guasto.
    /// </summary>
    private static readonly TimeSpan Freschezza = TimeSpan.FromSeconds(10);

    /// <summary>Categoria di log dei guasti di richiesta. Nome fisso: è la stringa da cercare nei log del
    /// server, quando i log del server si possono leggere. Fratello di <c>Vipi.Auth.Ivao</c>.</summary>
    public const string CategoriaLog = "Vipi.Errori";

    /// <summary>
    /// Aggiunge una riga al registro. <b>Non solleva mai</b>: un problema nel raccontare l'errore non deve
    /// diventare l'errore — e qui siamo già dentro la gestione di un guasto.
    /// </summary>
    public static void Registra(string? codice, string metodo, string percorso, string? utente, Exception ex)
    {
        try
        {
            var sb = new StringBuilder()
                .AppendLine()
                .AppendLine(new string('-', 78))
                .AppendLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC · codice {codice ?? "(nessuno)"}")
                .AppendLine($"{metodo} {percorso} · utente {utente ?? "non collegato"}")
                .AppendLine()
                .AppendLine(ex.ToString());

            // ⚠️ L'altra metà della storia. Lo stack dell'eccezione dice chi è MORTO; queste righe dicono chi
            // stava GIÀ CORRENDO sullo stesso DbContext. Senza, «A second operation was started» resta una
            // domanda — è successo il 24 agosto 2026, ed è costato un giro di deploy su un sospettato
            // sbagliato.
            //
            // 🔴 31 agosto 2026: qui si stampavano TUTTE le fotografie in coda, venti, a OGNI errore. Due
            // conseguenze, tutt'e due misurate sul file sceso dal server quel giorno:
            //   · 634 kB per TRE errori soli. Il tetto è 512 kB, quindi il file ruotava via dopo tre voci e
            //     la storia che serviva a capire non c'era già più.
            //   · la voce delle 11:40:17 portava fotografie delle 11:37:06 — di un'altra richiesta, con
            //     dentro query che con quella pagina non c'entravano. Una scena di un altro guasto allegata
            //     al tuo si legge come se fosse la tua: depista, e depista con l'aria del fatto.
            // Quindi: UNA fotografia, l'ultima, e solo se è di POCO PRIMA di questo errore.
            if (CollisioniDbContext.UltimoScatto(Freschezza) is { Length: > 0 } collisione)
            {
                sb.AppendLine().AppendLine("Che cosa era aperto sul DbContext quando è successo:");
                sb.AppendLine(collisione);
            }

            var voce = sb.ToString();

            lock (Serratura)
            {
                // Una nota gia' vista si scrive in una riga; la prima volta si scrive intera, perche' lo
                // stack di UN esemplare serve a capire da dove nasce quella famiglia.
                if (ENota(ex) && !NoteGiaViste.Add(Firma(ex)))
                    Scrivi(RigaDiNota(codice, metodo, percorso, utente, ex));
                else
                    Scrivi(voce);
            }
        }
        catch { /* non c'è un piano C, e non deve esserci */ }
    }

    /// <summary>
    /// Le famiglie di nota gia' incontrate da quando il processo e' partito: tipo dell'eccezione + primo
    /// fotogramma nostro. ⚠️ Non e' una cache che va svuotata: se il processo riparte, riparte anche questa,
    /// e il primo esemplare torna a scriversi intero.
    /// </summary>
    private static readonly HashSet<string> NoteGiaViste = new(StringComparer.Ordinal);

    /// <summary>
    /// Vero per i guasti che sono il <b>prezzo normale di una pagina che se ne va</b>: chi chiude la scheda
    /// mentre un caricamento e' in volo, e la query esplode su un contesto gia' smaltito o su un'attesa
    /// annullata.
    ///
    /// <para>🔴 <b>Perche' esiste questa distinzione.</b> Nel file sceso dal server il 7 settembre 2026,
    /// <b>39 voci su 71</b> erano <c>ObjectDisposedException</c> sul <c>VipiDbContext</c> di circuiti chiusi,
    /// e altre 12 <c>TaskCanceledException</c>. Non sono guasti; ma sono lunghe uno stack l'una, e i DUE
    /// difetti veri di quella finestra ci sono finiti in mezzo. E' la stessa lezione degli avvisi che suonano
    /// sul caso normale: <b>un registro fatto per meta' di rumore e' un registro che si smette di leggere</b>
    /// — e, peggio, che ruota via a 512 kB portandosi la storia che serviva.</para>
    ///
    /// <para>⚠️ <b>Non si demoliscono tutte le <c>ObjectDisposedException</c></b>, e la ragione ha una data:
    /// il 4 settembre 2026 il difetto era proprio una <c>ObjectDisposedException</c> — su un
    /// <c>SemaphoreSlim</c> smaltito, non su un <c>DbContext</c> — e abbatteva il circuito. Una regola scritta
    /// sul TIPO l'avrebbe nascosta. Qui la regola e' sull'OGGETTO smaltito: il contesto del database, cioe'
    /// l'unico che muore per un motivo previsto.</para>
    ///
    /// <para>⚠️ E una nota non sparisce: la prima di ogni famiglia si scrive intera, le successive in una
    /// riga che porta comunque tipo, percorso e primo fotogramma nostro. Dieci righe uguali si vedono; dieci
    /// stack uguali si saltano.</para>
    /// </summary>
    private static bool ENota(Exception ex) =>
        ex is OperationCanceledException
        || (ex is ObjectDisposedException smaltito
            && ((smaltito.ObjectName?.Contains("DbContext", StringComparison.OrdinalIgnoreCase) ?? false)
                || smaltito.Message.Contains("DbContext", StringComparison.OrdinalIgnoreCase)));

    /// <summary>La famiglia di una nota: il tipo e il punto NOSTRO da cui nasce. Due note con la stessa firma
    /// raccontano la stessa cosa, e la seconda in poi vale una riga.</summary>
    private static string Firma(Exception ex) => $"{ex.GetType().Name}|{PrimoFotogrammaNostro(ex)}";

    /// <summary>
    /// Il primo fotogramma di codice nostro nello stack: e' quel che serve per sapere DOVE, e sta in una
    /// riga. Se non ce n'e' nessuno lo dice, invece di lasciare un vuoto che sembra un dato mancante.
    /// </summary>
    private static string PrimoFotogrammaNostro(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            foreach (var riga in (e.StackTrace ?? "").Split('\n'))
            {
                var t = riga.Trim();
                if (!t.StartsWith("at Vipi.", StringComparison.Ordinal)) continue;
                var senzaAt = t[3..];
                var in_ = senzaAt.IndexOf(" in ", StringComparison.Ordinal);
                return in_ > 0 ? senzaAt[..in_] : senzaAt;
            }
        return "(nessun fotogramma nostro)";
    }

    /// <summary>Una nota gia' vista: una riga sola, e si riconosce a colpo d'occhio perche' comincia con
    /// <c>NOTA</c> invece che con la fila di trattini di una voce intera.</summary>
    private static string RigaDiNota(string? codice, string metodo, string percorso, string? utente, Exception ex) =>
        $"NOTA {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC · {ex.GetType().Name} · {PrimoFotogrammaNostro(ex)}"
        + $" · {metodo} {percorso} · utente {utente ?? "non collegato"} · codice {codice ?? "(nessuno)"}"
        + Environment.NewLine;

    private static void Scrivi(string voce)
    {
        if (StartupDiagnostics.Percorso(NomeFile) is not { } file)
        {
            Console.WriteLine("[vIPI] nessuna cartella scrivibile per il registro degli errori.");
            return;
        }

        try
        {
            var info = new FileInfo(file);
            if (info.Exists && info.Length > TettoByte && StartupDiagnostics.Percorso(NomeFilePrecedente) is { } vecchio)
                File.Move(file, vecchio, overwrite: true);

            if (!File.Exists(file))
                File.WriteAllText(file, Intestazione(), StartupDiagnostics.Codifica);

            File.AppendAllText(file, voce, StartupDiagnostics.Codifica);
        }
        catch (Exception errore)
        {
            Console.WriteLine($"[vIPI] impossibile scrivere {file}: {errore.Message}");
        }
    }

    /// <summary>Chi apre il file deve capire in tre righe che cos'è e che cosa farne.</summary>
    private static string Intestazione() =>
        $"""
        vIPI — richieste finite in errore. Le più recenti stanno in fondo.

        Ogni voce porta il CODICE mostrato in pagina all'utente: se qualcuno manda la fotografia di una
        pagina d'errore, quel codice si cerca qui dentro. La stringa di query non viene registrata (su
        /signin-oidc conterrebbe una credenziale), e nemmeno cookie o intestazioni.

        Le righe che cominciano con NOTA non sono guasti: sono il prezzo normale di una pagina che se ne
        va — la scheda chiusa mentre un caricamento era in volo. La PRIMA di ogni famiglia si scrive intera,
        con il suo stack; dalla seconda in poi vale una riga. Se una famiglia di note diventa fitta, e' un
        dato anche quello: dice che qualcuno se ne va sempre nello stesso punto.

        Il file si mette da parte come {NomeFilePrecedente} quando supera {TettoByte / 1024} kB.

        """;


    /// <summary>
    /// Il guasto del <b>login</b> IVAO, nello stesso registro di tutto il resto.
    ///
    /// <para>🔴 <b>Perché esiste, e la data è il 10 settembre 2026.</b> Uno staffista ha fatto il login verso
    /// le 11:00 UTC e si è trovato davanti una pagina d'errore; nel file sceso dal server due ore dopo
    /// l'ultima voce era del <b>giorno prima</b>. Non era una copia vecchia — l'impronta lo escludeva: era
    /// che di quel guasto qui non si scriveva niente. <c>OnRemoteFailure</c> lo racconta a un
    /// <c>ILogger</c> di categoria <c>Vipi.Auth.Ivao</c>, e su <c>atc.it.ivao.aero</c> quel logger scrive su
    /// <c>stdout</c>, che lì è il vuoto. Il registro è nato per il login rotto del 23 agosto 2026, ed era
    /// rimasto l'unico guasto che non ci finiva.</para>
    ///
    /// <para>⚠️ <b>Che cosa NON entra.</b> La stringa di query, mai: su <c>/signin-oidc</c> porta il
    /// <c>code</c> OAuth e lo <c>state</c>, cioè credenziali. Restano il motivo (che è un insieme CHIUSO),
    /// l'errore dichiarato dal portale, se il giro aveva ancora le sue proprietà, se una sessione c'era già
    /// e dove si stava andando: è quello che il 23 agosto è costato una serata a ricostruire.</para>
    ///
    /// <para>⚠️ Come <see cref="Registra"/>, <b>non solleva mai</b>: un guasto nel raccontare il guasto
    /// riporterebbe esattamente alla pagina muta che tutto questo serve a togliere di mezzo.</para>
    /// </summary>
    public static void RegistraLogin(
        string motivo, string errorePortale, bool statoRecuperato, bool giaDentro, string ritorno,
        string? utente, Exception? guasto)
    {
        try
        {
            var voce = VoceDiLogin(motivo, errorePortale, statoRecuperato, giaDentro, ritorno, utente, guasto);
            lock (Serratura) Scrivi(voce);
        }
        catch { /* non c'è un piano C, e non deve esserci */ }
    }

    /// <summary>
    /// Il testo della voce, separato dalla scrittura perché è la parte che si può provare: che ci sia il
    /// motivo, e che non ci finisca mai un pezzo di credenziale.
    /// <para>⚠️ Nessuna dedup da <see cref="ENota"/> qui: un login che fallisce non è mai rumore, e sono
    /// pochi per definizione — chi non entra non riprova venti volte al minuto.</para>
    /// </summary>
    internal static string VoceDiLogin(
        string motivo, string errorePortale, bool statoRecuperato, bool giaDentro, string ritorno,
        string? utente, Exception? guasto) =>
        new StringBuilder()
            .AppendLine()
            .AppendLine(new string('-', 78))
            .AppendLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC · codice login-{motivo}")
            .AppendLine($"LOGIN {PercorsoCallback} · utente {utente ?? "non collegato"}")
            .AppendLine()
            .AppendLine($"Motivo ..................... {motivo}")
            .AppendLine($"Errore dal portale ......... {errorePortale}")
            .AppendLine($"Stato del giro recuperato .. {(statoRecuperato ? "sì" : "NO")}")
            .AppendLine($"Sessione già attiva ........ {(giaDentro
                ? "sì — l'utente è rimasto dentro e NON ha visto niente"
                : "no — è finito sulla pagina che spiega")}")
            .AppendLine($"Ritorno .................... {ritorno}")
            .AppendLine()
            .AppendLine(guasto?.ToString()
                ?? "(nessuna eccezione: il giro si è fermato per una risposta del portale, non per un guasto nostro)")
            .ToString();

    /// <summary>
    /// La pagina d'errore è stata servita <b>senza</b> che ci fosse un'eccezione dietro: qualcuno è
    /// arrivato su <c>/Error</c> per la sua strada, non per <c>UseExceptionHandler</c>.
    ///
    /// <para>🔴 <b>Perché vale una riga.</b> Il 10 settembre 2026 un socio ha visto la pagina d'errore e
    /// nel registro non c'era niente per quell'ora. Le due spiegazioni — «la riga non si è scritta» e «non
    /// c'era nessuna eccezione da scrivere» — portano a due indagini opposte, e dalla pagina non si
    /// distinguono: è la stessa. Da qui in poi il file lo dice.</para>
    ///
    /// <para>Una riga sola, come le note: non è un guasto, è un fatto che serve a leggere gli altri.</para>
    /// </summary>
    public static void RegistraPaginaSenzaEccezione(string? codice, string percorso)
    {
        try
        {
            var riga = $"NOTA {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC · pagina /Error servita SENZA eccezione"
                       + $" (nessuna richiesta è morta: ci si è arrivati da {percorso})"
                       + $" · codice {codice ?? "(nessuno)"}" + Environment.NewLine;
            lock (Serratura) Scrivi(riga);
        }
        catch { /* non c'è un piano C, e non deve esserci */ }
    }

    /// <summary>Il percorso del callback, scritto a mano e senza query: la query è la credenziale.</summary>
    private const string PercorsoCallback = "/signin-oidc";

    /// <summary>
    /// Il gancio: registra ogni eccezione non gestita e <b>non</b> scrive la risposta — quella resta a
    /// <c>UseExceptionHandler("/Error")</c>, cioè a <see cref="PaginaErrore"/>. Ritornare <c>false</c> è
    /// proprio questo: «l'ho annotata, la pagina falla tu».
    /// </summary>
    internal sealed class Gancio : IExceptionHandler
    {
        private readonly ILogger _log;
        public Gancio(ILoggerFactory fabbrica) => _log = fabbrica.CreateLogger(CategoriaLog);

        public ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
        {
            // Lo STESSO identificativo che la pagina mostra: è il filo fra la fotografia e lo stack trace.
            var codice = Activity.Current?.Id ?? ctx.TraceIdentifier;
            var utente = Utente(ctx);

            // ⚠️ `ctx.Request.Path` qui vale GIA' «/Error»: il middleware riscrive il percorso PRIMA di
            // chiamare i gestori, e un registro che dicesse sempre «/Error» non direbbe niente. Il percorso
            // vero lo conserva la feature. La stringa di query non si tocca: su /signin-oidc è il `code`.
            var percorso = ctx.Features.Get<IExceptionHandlerPathFeature>()?.Path
                           ?? ctx.Request.Path.Value ?? "/";

            // Nel log del processo ci va comunque: dove i log si leggono, è lì che si guarda per primo.
            _log.LogError(ex, "Richiesta fallita — {Metodo} {Percorso}, codice {Codice}, utente {Utente}.",
                ctx.Request.Method, percorso, codice, utente ?? "non collegato");

            Registra(codice, ctx.Request.Method, percorso, utente, ex);

            return ValueTask.FromResult(false);
        }

        /// <summary>
        /// Il VID di chi ha ricevuto l'errore, letto dai claim e non da <c>ICurrentUserProvider</c>: qui
        /// siamo dentro la gestione di un guasto, e risolvere un servizio è un modo in più di fallire.
        /// ⚠️ Serve davvero: il difetto del 24 agosto 2026 si vedeva <b>solo</b> da loggati, e senza questa
        /// riga il registro non avrebbe detto la cosa che spiegava tutto.
        /// </summary>
        private static string? Utente(HttpContext ctx)
        {
            try
            {
                if (ctx.User?.Identity?.IsAuthenticated != true) return null;
                var vid = ctx.User.FindFirst("id")?.Value
                          ?? ctx.User.FindFirst("sub")?.Value
                          ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return vid is null ? "collegato (VID sconosciuto)" : $"VID {vid}";
            }
            catch { return null; }
        }
    }
}
