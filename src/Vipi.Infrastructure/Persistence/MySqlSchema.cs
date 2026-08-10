namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Dettagli del ramo MariaDB/MySQL che servono sia alla DI sia agli strumenti di migrazione.
/// </summary>
public static class MySqlSchema
{
    /// <summary>
    /// Dove vivono le migrazioni. Il nome è una stringa e non un <c>typeof</c> per una ragione di
    /// dipendenze: il progetto delle migrazioni referenzia <c>Vipi.Infrastructure</c> — è di lì che viene
    /// il <c>VipiDbContext</c> — quindi il riferimento inverso sarebbe un ciclo. EF risolve l'assembly per
    /// nome a runtime, e basta che sia nell'output dell'applicazione (ce lo mette l'host, che lo referenzia).
    ///
    /// <para>Il prezzo di una stringa è che una rinomina del progetto non rompe la compilazione: romperebbe
    /// l'avvio, con un messaggio EF che parla di migrazioni mancanti invece che di un assembly introvabile.
    /// Per questo <c>MySqlMigrationsTests</c> verifica che questo nome coincida con quello vero.</para>
    /// </summary>
    public const string MigrationsAssemblyName = "Vipi.Infrastructure.MySqlMigrations";

    /// <summary>Config opzionale per fissare la versione del server, es. <c>11.4.10</c>.</summary>
    public const string ServerVersionConfigKey = "Persistence:ServerVersion";

    /// <summary>
    /// Versione di MariaDB assunta quando la configurazione non ne indica una: quella del server di
    /// produzione, verificata con <c>SELECT VERSION()</c> il 5 agosto 2026 (<c>11.4.10-MariaDB-deb11</c>).
    /// </summary>
    public static readonly Version DefaultMariaDbVersion = new(11, 4, 10);

#if NET8_0
    /// <summary>
    /// Traduce il valore di configurazione in una <c>ServerVersion</c> per Pomelo. Assume <b>MariaDB</b>,
    /// non MySQL: è quello che gira su <c>atc.it.ivao.aero</c>, e le due famiglie divergono su cose che ci
    /// riguardano — la collation, e il fatto che <c>GET_LOCK(nome, -1)</c> su MariaDB torni NULL.
    ///
    /// <para>Un valore non interpretabile <b>non</b> viene ignorato in favore del default: fallisce. Una
    /// versione sbagliata cambia la SQL che il provider genera, e scoprirlo da una query storta è molto
    /// più caro che scoprirlo all'avvio.</para>
    /// </summary>
    public static Microsoft.EntityFrameworkCore.ServerVersion ResolveServerVersion(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
            return new Microsoft.EntityFrameworkCore.MariaDbServerVersion(DefaultMariaDbVersion);

        if (!Version.TryParse(configuredValue.Trim(), out var versione))
            throw new InvalidOperationException(
                $"{ServerVersionConfigKey} '{configuredValue}' non è una versione valida. " +
                "Attesa una forma tipo '11.4.10' (da SELECT VERSION() sul server MariaDB).");

        return new Microsoft.EntityFrameworkCore.MariaDbServerVersion(versione);
    }
#endif
}
