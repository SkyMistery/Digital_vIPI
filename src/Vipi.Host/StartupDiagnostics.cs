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
    /// Riepilogo della configurazione, senza segreti. Va chiamato appena il builder esiste: se l'avvio
    /// morisse dopo, questo file racconta comunque con quale configurazione ci ha provato.
    /// </summary>
    public static void WriteConfigurationSummary(WebApplicationBuilder builder)
    {
        var cfg = builder.Configuration;
        var sb = new StringBuilder();

        sb.AppendLine($"vIPI — diagnostica di avvio, {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(new string('-', 70));
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

        sb.AppendLine("Configurazione letta (i valori segreti non vengono riportati):");
        sb.AppendLine($"  Persistence:Provider ....... {Mostra(cfg["Persistence:Provider"], "assente ⇒ ricade su SQLite!")}");
        sb.AppendLine($"  ConnectionStrings:Vipi ..... {SenzaPassword(cfg.GetConnectionString("Vipi"))}");
        sb.AppendLine($"  VipiAuth:Enabled ........... {Mostra(cfg["VipiAuth:Enabled"], "assente ⇒ nessun login, /vsop/auth/login darà 404")}");
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
    /// Scrive accanto all'eseguibile; se la cartella non è scrivibile — capita con host che la montano in
    /// sola lettura — ripiega sulla temporanea, e in ogni caso stampa dove è finito. Non solleva mai: un
    /// problema nel raccontare l'errore non deve diventare l'errore.
    /// </summary>
    private static void Write(string nomeFile, string contenuto)
    {
        foreach (var cartella in new[] { AppContext.BaseDirectory, Path.GetTempPath() })
        {
            try
            {
                var percorso = Path.Combine(cartella, nomeFile);
                // UTF-8 CON BOM: questi file finiscono per email e vengono aperti col Blocco note su
                // Windows, che senza BOM li interpreta in ANSI e sfregia ogni accento. Il BOM non dà
                // fastidio agli editor seri né a `cat`.
                File.WriteAllText(percorso, contenuto, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                Console.WriteLine($"[vIPI] diagnostica scritta in {percorso}");
                return;
            }
            catch { /* cartella non scrivibile: si prova la prossima */ }
        }

        Console.WriteLine($"[vIPI] impossibile scrivere {nomeFile}: nessuna cartella scrivibile.");
        Console.WriteLine(contenuto);
    }
}
