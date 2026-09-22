using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Vipi.SectorLab.Ui.Server;

namespace Vipi.SectorLab;

// ⚠️ Qui dentro «Application» sarebbe il namespace Vipi.Application (arriva con Vipi.SectorLab.Core), che si trova
// salendo dal namespace Vipi PRIMA di un alias globale: Application.Run non compila. L'alias nel namespace vince.
using WinForms = System.Windows.Forms;

/// <summary>
/// L'ingresso di Aurora Sector Lab (carta F3 §2.1, §3): avvia il server Blazor locale e apre la finestra che lo mostra.
/// <para><c>--autoprova</c>: parte, aspetta che il circuito sia vivo e si chiude, con codice d'uscita 0 se tutto è
/// andato e 1 se no. È la prova d'avvio della slice 1, e si rifà su ogni consegna (lanciata da un'altra cartella).</para>
/// </summary>
internal static class Program
{
    /// <summary>La cartella dell'utente per l'app: diario d'avvio, dati della WebView2 e (slice 9) i backup.</summary>
    internal static readonly string CartellaDellUtente =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VipiSectorLab");

    [STAThread]
    private static int Main(string[] args)
    {
        var orologio = Stopwatch.StartNew();
        Directory.CreateDirectory(CartellaDellUtente);
        var diario = new Diario(Path.Combine(CartellaDellUtente, "avvio.txt"), orologio);
        bool autoprova = args.Contains("--autoprova", StringComparer.OrdinalIgnoreCase);
        diario.Scrivi($"Aurora Sector Lab {Versione.Testo} · .NET {Environment.Version} · cartella {AppContext.BaseDirectory} · lavoro {Environment.CurrentDirectory}");

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

        var server = ServerDelLab.Crea(new SegretoDelLab(), typeof(Program).Assembly.GetName().Name);
        server.StartAsync().GetAwaiter().GetResult();
        diario.Scrivi($"server in ascolto su {ServerDelLab.Indirizzo(server)}");

        int esito;
        try
        {
            using var finestra = new Finestra(ServerDelLab.Ingresso(server), diario, autoprova);
            WinForms.Application.Run(finestra);
            esito = finestra.Esito;
        }
        finally
        {
            server.StopAsync().GetAwaiter().GetResult();
            diario.Scrivi("chiuso");
        }
        return esito;
    }
}
