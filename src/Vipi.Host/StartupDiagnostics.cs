using System.Text;

namespace Vipi.Host;

/// <summary>
/// Scrive su file l'esito dell'avvio, per gli host dove <b>non si ha accesso ai log</b> del processo —
/// niente <c>journalctl</c>, niente console, solo FTP o un pannello.
///
/// <para><b>Perché esiste.</b> Un avvio fallito su un host così è cieco da entrambe le parti: chi gestisce
/// la macchina vede un servizio che non parte, chi ha scritto il codice non vede niente. Un'eccezione di
/// avvio finisce su <c>stderr</c>, che in quello scenario nessuno legge. Due file scaricabili chiudono
/// la questione in un giro invece che in tre.</para>
///
/// <para><b>Nessun segreto nei file.</b> Password, ClientId e ClientSecret non vengono mai scritti: della
/// configurazione si riporta solo <i>se</i> un valore c'è, non quale. La connection string è riportata
/// senza la password. È il motivo per cui questi file si possono spedire per email senza pensarci.</para>
/// </summary>
public static class StartupDiagnostics
{
    /// <summary>Riepilogo della configurazione vista all'avvio, riscritto a ogni avvio riuscito o fallito.</summary>
    public const string InfoFileName = "avvio-diagnostica.txt";

    /// <summary>Eccezione che ha impedito l'avvio. Se questo file NON c'è, l'app non è arrivata a scriverlo.</summary>
    public const string CrashFileName = "avvio-errore.txt";

    /// <summary>
    /// Registra la scrittura dell'eccezione fatale. Da chiamare come <b>prima</b> istruzione di
    /// <c>Program.cs</c>: quello che succede prima di qui non è coperto.
    /// </summary>
    public static void HookFatalErrors()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Write(CrashFileName, Describe(ex));
        };
    }

    /// <summary>
    /// Scrive l'eccezione che ha impedito l'avvio. Da chiamare dal <c>catch</c> attorno al corpo dell'avvio
    /// in <c>Program.cs</c>: il gancio di <see cref="HookFatalErrors"/> copre i guasti sugli altri thread,
    /// questo copre quelli sul thread principale — <b>compreso il caricamento tipi</b>, che avviene alla
    /// preparazione del metodo e che il gancio non vede mai. Vedi <c>VipiStartup</c>.
    /// </summary>
    public static void WriteFatal(Exception ex) => Write(CrashFileName, Describe(ex));

    /// <summary>
    /// Riepilogo della configurazione, senza segreti. Va chiamato appena il builder esiste: se l'avvio
    /// morisse dopo, questo file racconta comunque con quale configurazione ci ha provato.
    /// </summary>
    public static void WriteConfigurationSummary(WebApplicationBuilder builder, int fileSegretiLetti = 0)
    {
        var cfg = builder.Configuration;
        var sb = new StringBuilder();

        sb.AppendLine($"vIPI — diagnostica di avvio, {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(new string('-', 70));
        // ⚠️ QUALE codice è ripartito, non solo QUANDO: la data qui sopra si rinfresca a ogni riavvio —
        // e Passenger ne fa da solo, per inattività — quindi da sola non prova che sia arrivata la
        // versione nuova. Vedi VersioneBuild.
        sb.AppendLine($"Versione ..................... {VersioneBuild.Leggi().Dettaglio}");
        sb.AppendLine($"Ambiente ..................... {builder.Environment.EnvironmentName}");
        sb.AppendLine($"Cartella dell'applicazione ... {AppContext.BaseDirectory}");
        sb.AppendLine();

        // Il punto che decide se appsettings.Production.json viene letto o ignorato in silenzio.
        if (!string.Equals(builder.Environment.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("⚠ L'ambiente NON è Production: appsettings.Production.json viene IGNORATO, senza errori.")
              .AppendLine("  Impostare la variabile ASPNETCORE_ENVIRONMENT=Production.")
              .AppendLine();

        var fileProduzione = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");
        sb.AppendLine($"appsettings.Production.json .. {(File.Exists(fileProduzione) ? "presente" : "ASSENTE")}");
        sb.AppendLine();

        // ⚠️ Si scrive QUANTI file, mai QUALI: questo riepilogo è a sua volta scaricabile dal web sul
        // server vero (docs/lavori-aperti.md §A13), e il nome di quei file è l'unica cosa che li protegge.
        sb.AppendLine(fileSegretiLetti > 0
            ? $"Cartella «{SegretiFuoriDalWeb.Cartella}» ....... {fileSegretiLetti} file letti (i nomi non si riportano)"
            : $"Cartella «{SegretiFuoriDalWeb.Cartella}» ....... nessun file: i valori qui sotto vengono tutti da appsettings*");
        sb.AppendLine();

        sb.AppendLine("Configurazione letta (i valori segreti non vengono riportati):");
        sb.AppendLine($"  Persistence:Provider ....... {Mostra(cfg["Persistence:Provider"], "assente ⇒ ricade su SQLite!")}");
        sb.AppendLine($"  ConnectionStrings:Vipi ..... {SenzaPassword(cfg.GetConnectionString("Vipi"))}");
        sb.AppendLine($"  VipiAuth:Enabled ........... {Mostra(cfg["VipiAuth:Enabled"], "assente ⇒ nessun login, /services/vsop/auth/login darà 404")}");
        sb.AppendLine($"  VipiAuth:ClientId .......... {Presenza(cfg["VipiAuth:ClientId"])}");
        sb.AppendLine($"  VipiAuth:ClientSecret ...... {Presenza(cfg["VipiAuth:ClientSecret"])}  (facoltativo: senza, client pubblico con PKCE)");
        sb.AppendLine($"  Ivao:ClientId .............. {Presenza(cfg["Ivao:ClientId"])}");
        sb.AppendLine($"  Ivao:ClientSecret .......... {Presenza(cfg["Ivao:ClientSecret"])}");

        if (!string.IsNullOrWhiteSpace(cfg["VipiAuth:ClientId"]) &&
            !string.Equals(cfg["VipiAuth:ClientId"], cfg["Ivao:ClientId"], StringComparison.Ordinal))
            sb.AppendLine()
              .AppendLine("⚠ VipiAuth:ClientId e Ivao:ClientId sono DIVERSI. Di norma sono la stessa app IVAO:")
              .AppendLine("  se uno dei due è sbagliato il login funziona lo stesso, ma ATC live, roster e import no.");

        Write(InfoFileName, sb.ToString());
    }

    private static string Mostra(string? valore, string seAssente) =>
        string.IsNullOrWhiteSpace(valore) ? seAssente : valore;

    private static string Presenza(string? valore) =>
        string.IsNullOrWhiteSpace(valore) ? "VUOTO" : $"valorizzato ({valore.Length} caratteri)";

    /// <summary>La connection string serve per diagnosticare host, porta e database: la password no.</summary>
    private static string SenzaPassword(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return "assente ⇒ ricade su un file SQLite locale!";

        var parti = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase) ||
                         p.TrimStart().StartsWith("Pwd", StringComparison.OrdinalIgnoreCase)
                ? "Password=***"
                : p.Trim());

        return string.Join(";", parti);
    }

    /// <summary>
    /// Quanto è durata ogni fase dell'avvio, scritto in coda a <see cref="InfoFileName"/>.
    ///
    /// <para><b>Perché.</b> Questo sito gira sotto Passenger, che spegne il processo quando nessuno lo
    /// usa: il primo visitatore dopo la pausa paga l'avvio intero, e «ci mette tanto a ripartire» è la
    /// prima cosa che si nota. Su questo host non c'è modo di profilare — niente shell, niente log del
    /// processo — quindi la domanda «tanto DOVE?» non aveva risposta, e la tentazione era rispondere per
    /// analogia («sarà la compilazione al volo») invece che con una misura.</para>
    ///
    /// <para>La misura, presa il 27 agosto 2026, dice un'altra cosa: <b>1 172 ms su ~1 300 sono database</b>
    /// (migrazioni e manutenzioni d'avvio), e il resto sono 120 ms in tutto. È il motivo per cui compilare
    /// in anticipo il pacchetto (ReadyToRun) è stato provato e scartato — vedi il commento in
    /// <c>Vipi.Host.csproj</c>.</para>
    ///
    /// <para>⚠️ Costa un <see cref="Stopwatch"/> e sei righe di testo per avvio. È scritto perché la
    /// prossima persona che vorrà accorciare l'avvio parta da un numero invece che da un'ipotesi.</para>
    /// </summary>
    public sealed class CronometroAvvio
    {
        private readonly System.Diagnostics.Stopwatch _fase = System.Diagnostics.Stopwatch.StartNew();
        private readonly List<(string Nome, long Ms)> _fasi = new();

        /// <summary>Chiude la fase in corso col nome dato e ne apre una nuova.</summary>
        public void Segna(string nome)
        {
            _fasi.Add((nome, _fase.ElapsedMilliseconds));
            _fase.Restart();
        }

        /// <summary>
        /// Aggiunge il riepilogo in coda al file di diagnostica. Va chiamato subito prima di mettersi in
        /// ascolto: da lì in poi il tempo non è più «avvio», è attesa.
        /// </summary>
        public void Scrivi()
        {
            if (_fasi.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Durata delle fasi d'avvio");
            sb.AppendLine(new string('-', 70));
            foreach (var (nome, ms) in _fasi)
                sb.AppendLine($"  {nome.PadRight(34, '.')} {ms,6} ms");
            sb.AppendLine($"  {"TOTALE".PadRight(34, '.')} {_fasi.Sum(f => f.Ms),6} ms");
            sb.AppendLine();
            sb.AppendLine("  Se il totale cresce, quasi sempre cresce una delle due voci di database:");
            sb.AppendLine("  le migrazioni o le manutenzioni. Il resto dell'avvio è un decimo del tempo.");

            Append(InfoFileName, sb.ToString());
        }
    }

    private static string Describe(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"vIPI — l'avvio è FALLITO, {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(new string('-', 70));
        sb.AppendLine("Il messaggio della prima riga è quasi sempre la causa; il resto serve a noi.");
        sb.AppendLine();
        sb.AppendLine(ex.ToString());
        return sb.ToString();
    }

    /// <summary>
    /// Sottocartella dei due file. <b>Non</b> accanto all'eseguibile, com'era fino all'11 agosto 2026:
    /// <see cref="CrashFileName"/> contiene lo stack trace intero — è il contenuto giusto per chi deve
    /// capire e quello sbagliato da lasciare in una cartella che, su un hosting a pannello o FTP, può
    /// stare dentro il documento radice. Una cartella sola è anche una riga sola da negare nel proxy:
    /// <c>location ~ ^/diagnostica/ { deny all; }</c>, che è in <c>deploy/atc-ivao/nginx-vipi.conf</c>.
    /// </summary>
    public const string CartellaDiagnostica = "diagnostica";

    /// <summary>
    /// Scrive nella sottocartella <see cref="CartellaDiagnostica"/> accanto all'eseguibile; se non è
    /// scrivibile — capita con host che montano l'applicazione in sola lettura — ripiega sulla temporanea,
    /// e in ogni caso stampa dove è finito. Non solleva mai: un problema nel raccontare l'errore non deve
    /// diventare l'errore.
    /// </summary>
    /// <summary>
    /// Come <see cref="Write"/>, ma in coda invece che sovrascrivendo: il riepilogo delle fasi si aggiunge
    /// al riassunto della configurazione, che è già stato scritto quando l'avvio comincia.
    /// ⚠️ Non solleva mai, per la stessa ragione di <see cref="Write"/>: un problema nel raccontare
    /// l'avvio non deve diventare un avvio fallito.
    /// </summary>
    private static void Append(string nomeFile, string contenuto)
    {
        if (Percorso(nomeFile) is not { } percorso) { Console.WriteLine(contenuto); return; }
        try { File.AppendAllText(percorso, contenuto, Codifica); }
        catch (Exception ex) { Console.WriteLine($"[vIPI] impossibile aggiornare {percorso}: {ex.Message}"); }
    }

    private static void Write(string nomeFile, string contenuto)
    {
        if (Percorso(nomeFile) is not { } percorso)
        {
            Console.WriteLine($"[vIPI] impossibile scrivere {nomeFile}: nessuna cartella scrivibile.");
            Console.WriteLine(contenuto);
            return;
        }

        try
        {
            File.WriteAllText(percorso, contenuto, Codifica);
            Console.WriteLine($"[vIPI] diagnostica scritta in {percorso}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[vIPI] impossibile scrivere {percorso}: {ex.Message}");
            Console.WriteLine(contenuto);
        }
    }

    /// <summary>
    /// UTF-8 <b>CON BOM</b>: questi file finiscono per email e vengono aperti col Blocco note su Windows,
    /// che senza BOM li interpreta in ANSI e sfregia ogni accento. Il BOM non dà fastidio agli editor seri
    /// né a <c>cat</c>.
    /// </summary>
    internal static readonly UTF8Encoding Codifica = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// Percorso di un file dentro <see cref="CartellaDiagnostica"/>, creando la cartella; <c>null</c> se non
    /// c'è nessuna radice scrivibile. Accanto all'eseguibile se si può — è la cartella che si raggiunge via
    /// FTP, l'unico accesso che c'è su <c>atc.it.ivao.aero</c> — altrimenti la temporanea, dove almeno una
    /// shell la trova. Condiviso con <see cref="DiagnosticaErrori"/>: un solo posto dove guardare.
    /// </summary>
    internal static string? Percorso(string nomeFile)
    {
        foreach (var radice in new[] { AppContext.BaseDirectory, Path.GetTempPath() })
        {
            try
            {
                var cartella = Path.Combine(radice, CartellaDiagnostica);
                Directory.CreateDirectory(cartella);
                return Path.Combine(cartella, nomeFile);
            }
            catch { /* cartella non scrivibile: si prova la prossima */ }
        }
        return null;
    }
}
