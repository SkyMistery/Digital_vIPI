using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Vipi.SectorLab.Ui.Server;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab;

// ⚠️ Qui dentro «Application» sarebbe il namespace Vipi.Application (arriva con Vipi.SectorLab.Core), che si trova
// salendo dal namespace Vipi PRIMA di un alias globale: Application.Run non compila. L'alias nel namespace vince.
using WinForms = System.Windows.Forms;

/// <summary>
/// L'ingresso di Aurora Sector Lab (carta F3 §2.1, §3): avvia il server Blazor locale e apre la finestra che lo mostra.
/// <para><c>--autoprova</c>: parte, aspetta che il circuito sia vivo e si chiude, con codice d'uscita 0 se tutto è
/// andato e 1 se no. È la prova d'avvio della slice 1, e si rifà su ogni consegna (lanciata da un'altra cartella).</para>
/// <para>Senza <c>--cartella</c>, se c'è l'ambiente di lavoro (il primo avvio fatto), riapre da sé il suo sector.</para>
/// <para><c>--cartella &lt;percorso&gt;</c>: apre subito quella cartella del sector, senza passare dalla schermata
/// d'apertura (un collegamento per l'AOD, e la misura della mappa sull'albero vero). Con <c>--tutti-gli-strati</c> li
/// accende tutti; con <c>--autoprova</c> la prova aspetta la riga «mappa disegnata» invece di «pronto».</para>
/// <para><c>--dati &lt;cartella&gt;</c>: i dati del Lab (ultima cartella, recenti, impostazioni, backup, registro, WebView2)
/// stanno lì invece che in <c>%LOCALAPPDATA%\VipiSectorLab</c>. Con una cartella vuota è il primo avvio di un AOD.
/// L'autoprova senza <c>--dati</c> usa una cartella temporanea sua.</para>
/// </summary>
internal static class Program
{
    /// <summary>La cartella dell'utente per l'app: diario d'avvio, dati della WebView2 e (slice 9) i backup.
    /// La decide <see cref="SessioneDelLab.CartellaDeiDati"/> all'avvio.</summary>
    internal static string CartellaDellUtente { get; private set; } = SessioneDelLab.CartellaDeiDatiDiBase;

    [STAThread]
    private static int Main(string[] args)
    {
        var orologio = Stopwatch.StartNew();
        bool autoprova = args.Contains("--autoprova", StringComparer.OrdinalIgnoreCase);
        CartellaDellUtente = SessioneDelLab.CartellaDeiDati(Valore(args, "--dati"), autoprova);
        Directory.CreateDirectory(CartellaDellUtente);
        var diario = new Diario(Path.Combine(CartellaDellUtente, "avvio.txt"), orologio);
        string? cartellaDaAprire = Valore(args, "--cartella");
        bool tuttiGliStrati = args.Contains("--tutti-gli-strati", StringComparer.OrdinalIgnoreCase);
        diario.Scrivi($"Aurora Sector Lab {Versione.Testo} · .NET {Environment.Version} · cartella {AppContext.BaseDirectory} · lavoro {Environment.CurrentDirectory} · dati {CartellaDellUtente}");

        WinForms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        WinForms.Application.EnableVisualStyles();
        WinForms.Application.SetCompatibleTextRenderingDefault(false);

        // ⚠️ Senza il runtime della WebView2 la finestra resterebbe bianca, o l'app cadrebbe col primo messaggio
        // incomprensibile. Su Windows 10/11 aggiornati c'è; se manca, lo si dice e si dice dove prenderlo.
        string? runtime;
        try
        {
            runtime = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            runtime = null;
        }
        if (runtime is null)
        {
            diario.Scrivi("WebView2 Runtime assente");
            if (!autoprova)
            {
                MessageBox.Show(
                    "Aurora Sector Lab ha bisogno di Microsoft Edge WebView2 Runtime, che su questo PC non c'è.\n\n" +
                    "Si scarica gratis da Microsoft: https://developer.microsoft.com/microsoft-edge/webview2/\n" +
                    "(«Evergreen Bootstrapper»). Poi riaprite il Lab.",
                    "Aurora Sector Lab", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return 3;
        }
        diario.Scrivi($"WebView2 Runtime {runtime}");

        var server = ServerDelLab.Crea(new SegretoDelLab(), typeof(Program).Assembly.GetName().Name, CartellaDellUtente,
            Vipi.SectorLab.Core.Mappa.SchemiDiAurora.CartellaDiAuroraDiBase);
        server.StartAsync().GetAwaiter().GetResult();
        diario.Scrivi($"server in ascolto su {ServerDelLab.Indirizzo(server)}");

        // Il registro (chiesto alle prove a mano, 23 settembre): l'avvio, e ogni errore che nessuno raccoglie — un
        // gestore WinForms, un filo, un task che nessuno aspetta. Senza, un'eccezione così sparisce con l'app.
        var registro = server.Services.GetRequiredService<Registro>();
        registro.Scrivi("avvio", $"Aurora Sector Lab {Versione.Testo} · .NET {Environment.Version} · WebView2 {runtime} · " +
                                 $"{AppContext.BaseDirectory} · argomenti «{string.Join(' ', args)}»");
        WinForms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        WinForms.Application.ThreadException += (_, e) => registro.Errore("finestra", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            registro.Scrivi("ERRORE", $"non gestito (l'app si chiude: {e.IsTerminating}): {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            registro.Errore("task non osservato", e.Exception);
            e.SetObserved();
        };

        // Il Lab lavora nel suo ambiente (20 ottobre): senza --cartella riapre da sé il sector dell'ambiente di lavoro.
        cartellaDaAprire ??= server.Services.GetRequiredService<SessioneDelLab>().SectorDaRiaprire();
        bool cartellaAperta = false;
        if (cartellaDaAprire is not null)
        {
            var lab = server.Services.GetRequiredService<SessioneDelLab>();
            var quanto = Stopwatch.StartNew();
            cartellaAperta = lab.ApriAsync(cartellaDaAprire).GetAwaiter().GetResult();
            diario.Scrivi(cartellaAperta
                ? $"cartella aperta: {cartellaDaAprire} · {lab.Sessione!.File.Count} file · " +
                  $"{lab.Strati.Sum(s => s.Forme.Count):N0} forme · {lab.Strati.Sum(s => s.Punti):N0} punti · {quanto.ElapsedMilliseconds} ms"
                : $"cartella NON aperta: {lab.Errore}");
            if (cartellaAperta && tuttiGliStrati)
            {
                foreach (var strato in lab.Strati.Where(s => s.Forme.Count > 0))
                    lab.Accendi(strato.Id, acceso: true);
            }
        }

        int esito;
        try
        {
            var sessione = server.Services.GetRequiredService<SessioneDelLab>();
            using var finestra = new Finestra(ServerDelLab.Ingresso(server), diario, autoprova, attendeLaMappa: cartellaAperta,
                lab: sessione);
            // «Sfoglia…» (18 ottobre): la finestra di Windows per scegliere una cartella, sul filo della finestra.
            sessione.ScegliCartella = (titolo, iniziale) => ScegliCartella(finestra, titolo, iniziale);
            // E per un file (prova A45: il PDF dell'emendamento).
            sessione.ScegliFile = (titolo, iniziale, filtro) => ScegliFile(finestra, titolo, iniziale, filtro);
            WinForms.Application.Run(finestra);
            esito = finestra.Esito;
        }
        finally
        {
            server.StopAsync().GetAwaiter().GetResult();
            diario.Scrivi("chiuso");
            server.Services.GetRequiredService<Registro>().Scrivi("avvio", "chiuso");
        }
        return esito;
    }

    /// <summary>
    /// La finestra di Windows per scegliere una cartella, aperta sul filo della finestra del Lab (la chiama il circuito
    /// Blazor, da un altro filo). Torna la cartella scelta, o null se l'AOD annulla.
    /// </summary>
    private static Task<string?> ScegliCartella(WinForms.Form finestra, string titolo, string? iniziale)
    {
        var scelta = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        finestra.BeginInvoke(() =>
        {
            try
            {
                using var dialogo = new WinForms.FolderBrowserDialog
                {
                    Description = titolo,
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true,
                };
                if (!string.IsNullOrWhiteSpace(iniziale) && Directory.Exists(iniziale))
                    dialogo.InitialDirectory = iniziale;
                scelta.SetResult(dialogo.ShowDialog(finestra) == WinForms.DialogResult.OK ? dialogo.SelectedPath : null);
            }
            catch (Exception e)
            {
                scelta.SetException(e);
            }
        });
        return scelta.Task;
    }

    /// <summary>
    /// La finestra di Windows per scegliere un file, come <see cref="ScegliCartella"/>: parte dalla cartella di
    /// <paramref name="iniziale"/> (un file) o da <paramref name="iniziale"/> stessa (una cartella). Torna il file, o null.
    /// </summary>
    private static Task<string?> ScegliFile(WinForms.Form finestra, string titolo, string? iniziale, string filtro)
    {
        var scelta = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        finestra.BeginInvoke(() =>
        {
            try
            {
                using var dialogo = new WinForms.OpenFileDialog
                {
                    Title = titolo,
                    Filter = filtro,
                    CheckFileExists = true,
                    Multiselect = false,
                };
                var cartella = File.Exists(iniziale) ? Path.GetDirectoryName(iniziale) : iniziale;
                if (!string.IsNullOrWhiteSpace(cartella) && Directory.Exists(cartella))
                    dialogo.InitialDirectory = cartella;
                if (File.Exists(iniziale))
                    dialogo.FileName = Path.GetFileName(iniziale);
                scelta.SetResult(dialogo.ShowDialog(finestra) == WinForms.DialogResult.OK ? dialogo.FileName : null);
            }
            catch (Exception e)
            {
                scelta.SetException(e);
            }
        });
        return scelta.Task;
    }

    /// <summary>Il valore di un argomento con un valore: <c>--cartella D:\…</c>. Nullo se non c'è o è l'ultimo.</summary>
    private static string? Valore(string[] args, string nome)
    {
        int dove = Array.FindIndex(args, a => string.Equals(a, nome, StringComparison.OrdinalIgnoreCase));
        return dove >= 0 && dove + 1 < args.Length ? args[dove + 1] : null;
    }
}
