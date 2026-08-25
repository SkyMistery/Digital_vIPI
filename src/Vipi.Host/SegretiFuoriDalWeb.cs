namespace Vipi.Host;

/// <summary>
/// Configurazione riservata letta da file che <b>non hanno un nome indovinabile</b>.
///
/// <para>✅ <b>Aggiornamento 25 agosto 2026:</b> l'hosting ha chiuso l'accesso ai file — la cartella dell'app
/// non è più servita dal filesystem, le richieste passano tutte all'applicazione e i file alla radice
/// rispondono 404 (verificato dall'esterno). Questo meccanismo <b>resta</b> lo stesso: tenere i segreti fuori
/// da <c>appsettings.Production.json</c> è difesa in profondità, e l'assetto dell'hosting è già cambiato due
/// volte — se cambia di nuovo, questo file non deve tornare a essere una miniera.</para>
///
/// <para><b>Perché esiste — misurato il 24 agosto 2026.</b> All'epoca, su <c>atc.it.ivao.aero</c> la cartella
/// dell'applicazione <b>era</b> il document root del sito: il server davanti serviva i file da sé, prima del
/// proxy, e <c>/appsettings.Production.json</c> rispondeva <b>200</b>. Dentro quel file stanno la password del
/// database e le credenziali IVAO. Non era un difetto dell'applicazione — nessuna riga può intercettare una
/// richiesta che non le arriva mai. La strada messa in opera: <b>far sì che il file scaricabile non contenga
/// più niente di segreto</b>. ⚠️ I segreti esposti fino al 25 agosto vanno comunque ruotati.</para>
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

    /// <summary>
    /// I nomi accettati, in ordine di precedenza. <c>secrets</c> non è un capriccio: la <b>prima persona</b>
    /// che ha seguito il foglio, il 24 agosto 2026, ha chiamato così la cartella — e l'applicazione le ha
    /// risposto «nessun file», che è vero e non aiuta. ⚠️ Su Linux il nome è anche <b>sensibile alle
    /// maiuscole</b>, quindi il modo di sbagliare non è raro: costa tre righe accettarlo, e chi installa
    /// non è detto che parli italiano.
    /// </summary>
    private static readonly string[] Cartelle = { Cartella, "secrets" };

    /// <summary>Il segnaposto del foglio di configurazione: se arriva fin qui, nessuno l'ha sostituito.</summary>
    internal const string Segnaposto = "METTI-QUI-LA-PASSWORD";

    /// <summary>
    /// Unisce alla configurazione ogni <c>*.json</c> della cartella, in ordine di nome. Ritorna quanti file
    /// ha letto (0 = cartella assente o vuota, ed è il caso normale in sviluppo e nei test).
    /// </summary>
    internal static int Carica(IConfigurationBuilder configurazione)
    {
        var letti = 0;
        foreach (var nome in Cartelle)
        {
            var cartella = Path.Combine(AppContext.BaseDirectory, nome);
            if (!Directory.Exists(cartella)) continue;

            // Ordine per nome: con due file che dicono la stessa chiave, deve vincere sempre lo stesso — e
            // «l'ordine in cui il filesystem li elenca» non è un criterio, è il caso.
            foreach (var file in Directory.EnumerateFiles(cartella, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                configurazione.AddJsonFile(file, optional: true, reloadOnChange: false);
                letti++;
            }
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
                 + "(o «secrets») accanto all'eseguibile (vedi il foglio di correzione).";

        if (connectionString.Contains(Segnaposto, StringComparison.OrdinalIgnoreCase))
            return $"La connection string «Vipi» contiene ancora il segnaposto «{Segnaposto}»: la password "
                 + $"vera va messa in un file .json dentro la cartella «{Cartella}» accanto all'eseguibile, "
                 + "NON in appsettings.Production.json — i segreti vanno tenuti fuori da quel file per principio.";

        return null;
    }

    /// <summary>Applica <see cref="ValidaConnessione"/>: lancia se la configurazione non è utilizzabile.</summary>
    internal static void EnsureConnessioneUsabile(string? provider, string? connectionString)
    {
        if (ValidaConnessione(provider, connectionString) is { } errore)
            throw new InvalidOperationException(errore);
    }
}
