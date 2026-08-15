namespace Vipi.Application.Diagnostics;

/// <summary>
/// Verifica le due impostazioni del server di database che l'applicazione <b>assume</b> senza poterle
/// imporre. Nessuna delle due si può decidere da qui: stanno nella configurazione del server, che su
/// <c>atc.it.ivao.aero</c> è del committente.
///
/// <list type="number">
///   <item><description><b><c>sql_mode</c></b> — senza <c>STRICT_TRANS_TABLES</c> un valore troppo lungo per
///   la sua colonna <b>non fallisce: tronca in silenzio</b>. Le lunghezze del modello sono dimensionate con
///   margine sui dati veri, quindi il caso è remoto — ma se accade, il dato è già corrotto quando qualcuno
///   se ne accorge, e un enum troncato non si rilegge più. Sugli hosting condivisi lo strict mode spento è
///   comune.</description></item>
///   <item><description><b><c>max_allowed_packet</c></b> — le immagini dei blocchi sono <c>longblob</c> e il
///   taglio applicativo è a 3 MB, quindi il server ne deve accettare almeno 4. Il tetto <b>si supera in un
///   colpo solo</b>, il giorno in cui qualcuno carica una carta grande, e l'errore che esce
///   (<c>Got a packet bigger than 'max_allowed_packet' bytes</c>) non somiglia a «l'immagine è troppo
///   grande».</description></item>
/// </list>
///
/// <para><b>Perché una sonda e non una domanda al committente.</b> Erano due voci aperte dell'elenco A9, in
/// attesa di una risposta via mail. Una risposta di quel tipo vale il giorno in cui arriva: il
/// <c>sql_mode</c> lo cambia un aggiornamento del pacchetto, e nessuno riscriverebbe la mail. Una sonda dice
/// com'è <b>adesso</b>, e continua a dirlo.</para>
///
/// <para>Confluisce nel report di consistenza, quindi si legge da <c>/vsop/admin/diagnostica</c> e da
/// <c>/vsop/health</c> (→ Degraded). <b>Non</b> dalla sonda <c>ready</c>: quella dev'essere economica, e
/// queste sono condizioni che non cambiano da un secondo all'altro.</para>
/// </summary>
public interface IServerSettingsProbe
{
    /// <summary>Vuoto se il server è configurato come serve — o se il provider non è MySQL/MariaDB, dove la
    /// domanda non si pone affatto.</summary>
    Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default);
}

/// <summary>
/// Le soglie, separate dalla lettura del server perché il giudizio è una funzione pura: si prova senza un
/// database, e i due valori arrivano da una query sola.
/// </summary>
public static class ServerSettingsAnalyzer
{
    /// <summary>Categoria con cui le segnalazioni compaiono nella diagnostica.</summary>
    public const string Category = "Configurazione del server";

    /// <summary>
    /// Minimo per <c>max_allowed_packet</c>: 4 MiB. Il taglio applicativo agli upload è 3 MB
    /// (<c>Media:MaxUploadBytes</c>); il margine copre l'overhead del protocollo attorno al blob.
    /// </summary>
    public const int MinMaxAllowedPacket = 4 * 1024 * 1024;

    /// <summary>La modalità che rende un troncamento un errore invece che un silenzio.</summary>
    public const string StrictMode = "STRICT_TRANS_TABLES";

    /// <param name="sqlMode">Valore di <c>@@sql_mode</c> (lista separata da virgole), o null se illeggibile.</param>
    /// <param name="maxAllowedPacket">Valore di <c>@@max_allowed_packet</c> in byte, o null se illeggibile.</param>
    public static IReadOnlyList<ConsistencyFinding> Analyze(string? sqlMode, long? maxAllowedPacket)
    {
        var findings = new List<ConsistencyFinding>();

        // Un valore illeggibile NON è un valore buono: si segnala, perché il silenzio qui somiglia troppo
        // all'esito «tutto a posto».
        if (sqlMode is null)
        {
            findings.Add(new ConsistencyFinding(Category, ConsistencySeverity.Warning, "sql_mode",
                "Non è stato possibile leggere @@sql_mode: impossibile dire se una scrittura troppo lunga " +
                "fallirebbe o troncherebbe in silenzio."));
        }
        else if (!sqlMode.Split(',').Any(m => m.Trim().Equals(StrictMode, StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new ConsistencyFinding(Category, ConsistencySeverity.Error, "sql_mode",
                $"Il server NON è in strict mode (@@sql_mode = «{sqlMode}»): un valore più lungo della sua " +
                $"colonna viene troncato in silenzio invece di fallire. Serve {StrictMode}. " +
                "Si corregge nella configurazione del server, non da qui."));
        }

        if (maxAllowedPacket is null)
        {
            findings.Add(new ConsistencyFinding(Category, ConsistencySeverity.Warning, "max_allowed_packet",
                "Non è stato possibile leggere @@max_allowed_packet: impossibile dire se il caricamento di " +
                "un'immagine al limite dei 3 MB andrebbe a buon fine."));
        }
        else if (maxAllowedPacket < MinMaxAllowedPacket)
        {
            findings.Add(new ConsistencyFinding(Category, ConsistencySeverity.Error, "max_allowed_packet",
                $"@@max_allowed_packet = {maxAllowedPacket} byte, sotto il minimo di {MinMaxAllowedPacket} " +
                "(4 MiB) richiesto dalle immagini dei blocchi, che l'app accetta fino a 3 MB. Il " +
                "caricamento di un'immagine grande fallirà con «Got a packet bigger than " +
                "'max_allowed_packet' bytes». Si corregge nella configurazione del server."));
        }

        return findings;
    }
}
