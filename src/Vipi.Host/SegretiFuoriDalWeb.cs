namespace Vipi.Host;

/// <summary>
/// Configurazione riservata letta da file che <b>non hanno un nome indovinabile</b>.
///
/// <para><b>Perché esiste — misurato il 24 agosto 2026.</b> Su <c>atc.it.ivao.aero</c> la cartella
/// dell'applicazione <b>è</b> il document root del sito: il server davanti serve i file da sé, prima del
/// proxy, e <c>/appsettings.Production.json</c> risponde <b>200</b>. Dentro quel file stanno la password del
/// database e le credenziali IVAO. Non è un difetto dell'applicazione — nessuna riga può intercettare una
/// richiesta che non le arriva mai — e non si può chiudere né ruotando i segreti né dal pannello, che non
/// c'è. Resta una sola strada: <b>far sì che il file scaricabile non contenga più niente di segreto</b>.</para>
///
/// <para><b>Come.</b> Ogni <c>*.json</c> dentro <see cref="Cartella"/> viene unito alla configurazione,
/// <b>dopo</b> tutto il resto: quindi vince su <c>appsettings.Production.json</c>. Il nome del file lo
/// sceglie chi installa e non è scritto da nessuna parte — è la stessa protezione che regge oggi il
/// key-ring (<c>vipi-keys/key-{guid}.xml</c>): il server non elenca le cartelle, quindi un file si può
/// prendere solo indovinandone il nome esatto.</para>
///
/// <para>⚠️ <b>È sicurezza per oscurità, e va detto.</b> Non è la soluzione — la soluzione è che il document
/// root non sia la cartella dell'applicazione — ma è ciò che si può fare avendo <b>solo l'FTP</b>, e sposta
/// i segreti da «scaricabili con un URL scritto nel nostro repo» a «scaricabili da chi indovina un nome che
/// nessuno conosce». Vedi <c>docs/lavori-aperti.md</c> §A13.</para>
///
/// <para>⚠️ Il nome dei file trovati <b>non</b> finisce nella diagnostica d'avvio: quel file è scaricabile,
/// e scriverci dentro il nome vanificherebbe tutto. Si riporta solo <b>quanti</b> ne sono stati letti.</para>
/// </summary>
internal static class SegretiFuoriDalWeb
{
    /// <summary>Sottocartella accanto all'eseguibile. Il NOME DELLA CARTELLA è pubblico — quello dei file no.</summary>
    internal const string Cartella = "segreti";

    /// <summary>Il segnaposto del foglio di configurazione: se arriva fin qui, nessuno l'ha sostituito.</summary>
    internal const string Segnaposto = "METTI-QUI-LA-PASSWORD";

    /// <summary>
    /// Unisce alla configurazione ogni <c>*.json</c> della cartella, in ordine di nome. Ritorna quanti file
    /// ha letto (0 = cartella assente o vuota, ed è il caso normale in sviluppo e nei test).
    /// </summary>
    internal static int Carica(IConfigurationBuilder configurazione)
    {
        var cartella = Path.Combine(AppContext.BaseDirectory, Cartella);
        if (!Directory.Exists(cartella)) return 0;

        var letti = 0;
        // Ordine per nome: con due file che dicono la stessa chiave, deve vincere sempre lo stesso — e
        // «l'ordine in cui il filesystem li elenca» non è un criterio, è il caso.
        foreach (var file in Directory.EnumerateFiles(cartella, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            configurazione.AddJsonFile(file, optional: true, reloadOnChange: false);
            letti++;
        }
        return letti;
    }

    /// <summary>
    /// Ferma l'avvio se il provider è MySql ma la connessione non è utilizzabile. Logica pura, così la si
    /// può provare senza un host: ritorna il messaggio d'errore, o <c>null</c> se va bene.
    ///
    /// <para>⚠️ <b>Solo i due casi inequivocabili</b> — stringa assente, o segnaposto mai sostituito. Una
    /// password che non si riesce a riconoscere <b>non</b> ferma niente: le connection string MySQL hanno
    /// più modi legittimi di autenticarsi (socket unix, plugin, <c>pwd</c> invece di <c>password</c>), e una
    /// guardia troppo sveglia qui non protegge un dato — spegne il sito.</para>
    /// </summary>
    internal static string? ValidaConnessione(string? provider, string? connectionString)
    {
        if (!string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase)) return null;

        if (string.IsNullOrWhiteSpace(connectionString))
            return "Persistence:Provider è MySql ma non c'è nessuna connection string «Vipi». Partendo così "
                 + "l'applicazione ripiegherebbe su un file SQLite vuoto, e il sito sembrerebbe aver perso "
                 + "tutti i dati. Il valore va in un file .json dentro la cartella «" + Cartella + "» "
                 + "accanto all'eseguibile (vedi il foglio di correzione).";

        if (connectionString.Contains(Segnaposto, StringComparison.OrdinalIgnoreCase))
            return $"La connection string «Vipi» contiene ancora il segnaposto «{Segnaposto}»: la password "
                 + $"vera va messa in un file .json dentro la cartella «{Cartella}» accanto all'eseguibile, "
                 + "NON in appsettings.Production.json — quel file è scaricabile dal web.";

        return null;
    }

    /// <summary>Applica <see cref="ValidaConnessione"/>: lancia se la configurazione non è utilizzabile.</summary>
    internal static void EnsureConnessioneUsabile(string? provider, string? connectionString)
    {
        if (ValidaConnessione(provider, connectionString) is { } errore)
            throw new InvalidOperationException(errore);
    }
}
