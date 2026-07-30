namespace Vipi.Infrastructure.Persistence;

/// <summary>Provider di persistenza selezionabile (ADR-0007). Oggi solo <see cref="Sqlite"/> è operativo.</summary>
public enum PersistenceProvider
{
    /// <summary>SQLite su file (default): operativo. Tampone concorrenza via <see cref="SqliteTuningInterceptor"/>.</summary>
    Sqlite,

    /// <summary>PostgreSQL (cutover pianificato ADR-0007): non ancora operativo — servono migrazioni dedicate + validazione istanza.</summary>
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
