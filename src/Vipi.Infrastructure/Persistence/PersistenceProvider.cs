namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Provider di persistenza selezionabile (ADR-0007). Tutti e tre i valori sono operativi, ma servono a
/// tre scopi diversi: <see cref="Sqlite"/> è lo sviluppo, <see cref="Postgres"/> è il deploy di prova
/// Render+Neon, <see cref="MySql"/> è la <b>produzione</b> su <c>atc.it.ivao.aero</c>.
/// </summary>
public enum PersistenceProvider
{
    /// <summary>SQLite su file (default): operativo, con le migrazioni versionate. Tampone concorrenza via <see cref="SqliteTuningInterceptor"/>.</summary>
    Sqlite,

    /// <summary>
    /// PostgreSQL: operativo — è il deploy Render+Neon, che dopo il cutover resta come ambiente di prova e
    /// sorgente del travaso. Schema creato via <c>EnsureCreated</c> + <see cref="PostgresSchemaReconciler"/>,
    /// non dalle migrazioni (SQLite-flavored). Il cutover con migrazioni dedicate — ADR-0007 punto (b) —
    /// resta aperto: il reconciler copre solo le aggiunte di colonna.
    /// </summary>
    Postgres,

    /// <summary>
    /// MySQL 8.0+: il database di <b>produzione</b> (ADR-0007 §D4-bis). Provider Oracle
    /// <c>MySql.EntityFrameworkCore</c>, non Pomelo — che su EF Core 10 non esiste.
    /// <para>A differenza di Postgres lo schema NON si crea con <c>EnsureCreated</c> + reconciler ma da un
    /// set di migrazioni dedicato: la DDL di MySQL non è transazionale, quindi un reconcile interrotto
    /// lascerebbe lo schema parziale senza rollback. Il compromesso che accettiamo su Neon, che è casa
    /// nostra, non lo esportiamo sul database di un partner.</para>
    /// <para>Porta con sé due aggiustamenti al modello, applicati solo qui: le lunghezze delle colonne
    /// stringa indicizzate (<see cref="MySqlStringLengths"/>) e la collation case- e accent-sensitive
    /// (<see cref="MySqlCollation"/>).</para>
    /// </summary>
    MySql,
}

/// <summary>
/// Risolve il provider di persistenza dal valore di config <c>Persistence:Provider</c>. Puro e testabile:
/// default <see cref="PersistenceProvider.Sqlite"/> quando assente/vuoto; case-insensitive; valore sconosciuto
/// ⇒ eccezione con l'elenco dei valori validi (stesso pattern di <c>DataSource:Provider</c>).
/// </summary>
public static class PersistenceProviderResolver
{
    public const string ProviderConfigKey = "Persistence:Provider";

    public static PersistenceProvider Resolve(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue)) return PersistenceProvider.Sqlite;

        if (Enum.TryParse<PersistenceProvider>(configuredValue.Trim(), ignoreCase: true, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"Persistence:Provider '{configuredValue}' non supportato. Valori validi: Sqlite, Postgres, MySql.");
    }
}
