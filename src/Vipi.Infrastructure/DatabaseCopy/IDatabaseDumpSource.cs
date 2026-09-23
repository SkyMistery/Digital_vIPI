namespace Vipi.Infrastructure.DatabaseCopy;

/// <summary>
/// Chi sa leggere un database intero per la copia. Oggi uno solo, <c>MySqlDumpSource</c> (MariaDB, via
/// Pomelo/MySqlConnector); sugli altri provider non se ne registra nessuno e la copia risulta non disponibile.
/// </summary>
public interface IDatabaseDumpSource
{
    /// <summary>
    /// Apre la fotografia del database e prepara tutto quello che può fallire — connessione, elenco delle
    /// tabelle, le loro <c>CREATE</c> — <b>senza scrivere niente</b>. I dati si versano dopo, con
    /// <see cref="IDumpSnapshot.WriteTablesAsync"/>.
    /// </summary>
    Task<IDumpSnapshot> OpenAsync(CancellationToken ct = default);
}

/// <summary>Una fotografia aperta del database: finché vive, quello che legge è coerente.</summary>
public interface IDumpSnapshot : IAsyncDisposable
{
    string ServerVersion { get; }
    string? LastMigration { get; }

    /// <summary>Le tabelle che la copia lascia fuori di proposito, per scriverlo in testata.</summary>
    IReadOnlyList<string> Excluded { get; }

    /// <summary>Scrive tutte le tabelle, dalla <c>BeginTable</c> alla <c>EndTable</c>, e dopo di loro le viste
    /// condivise (<c>WriteView</c>). Non scrive né la testata né la chiusura, che sono di chi ha aperto lo
    /// scrittore.</summary>
    Task WriteTablesAsync(SqlDumpWriter writer, CancellationToken ct = default);
}
