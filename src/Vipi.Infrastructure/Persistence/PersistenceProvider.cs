namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Provider di persistenza selezionabile (ADR-0007). Entrambi i valori sono operativi.
/// MySQL non c'è: è la strada scelta per l'embedding in Ivao.It ma non è ancora implementata —
/// piano in <c>docs/design/piano-supporto-mysql.md</c>, decisione in ADR-0007 §D4 (sarà solo su net8).
/// </summary>
public enum PersistenceProvider
{
    /// <summary>SQLite su file (default): operativo, con le migrazioni versionate. Tampone concorrenza via <see cref="SqliteTuningInterceptor"/>.</summary>
    Sqlite,

    /// <summary>
    /// PostgreSQL: operativo — è il deploy Render+Neon. Schema creato via <c>EnsureCreated</c> +
    /// <see cref="PostgresSchemaReconciler"/>, non dalle migrazioni (SQLite-flavored). Il cutover con
    /// migrazioni dedicate — ADR-0007 punto (b) — resta aperto: il reconciler copre solo le aggiunte di colonna.
    /// </summary>
    Postgres,
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
            $"Persistence:Provider '{configuredValue}' non supportato. Valori validi: Sqlite, Postgres.");
    }
}
