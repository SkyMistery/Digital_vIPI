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
            // sbagliato. Si stampano solo se ce ne sono.
            if (CollisioniDbContext.Scatti_() is { Count: > 0 } collisioni)
            {
                sb.AppendLine().AppendLine("Che cosa era aperto sul DbContext quando è successo (il più recente per ultimo):");
                foreach (var c in collisioni) sb.AppendLine(c);
            }

            var voce = sb.ToString();

            lock (Serratura) Scrivi(voce);
        }
        catch { /* non c'è un piano C, e non deve esserci */ }
    }

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

        Il file si mette da parte come {NomeFilePrecedente} quando supera {TettoByte / 1024} kB.

        """;

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
