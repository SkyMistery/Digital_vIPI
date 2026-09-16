namespace Vipi.Application.Diagnostics;

/// <summary>Dove si scarica la copia. Sta qui perché la citano in due: l'indirizzo (<c>Vipi.Hosting</c>) e il
/// tasto in Diagnostica (<c>Vipi.Ui</c>), che non si vedono fra loro.</summary>
public static class DatabaseBackupRoute
{
    public const string Path = "/services/vsop/admin/diagnostics/database-backup";
}

/// <summary>Quello che una copia completata ha spedito: lo stesso che sta scritto nella riga di chiusura del
/// file, così chi ha il file in mano può confrontarlo col registro.</summary>
/// <param name="Tables">Tabelle copiate.</param>
/// <param name="Rows">Righe copiate, tutte le tabelle.</param>
/// <param name="Bytes">Byte del testo SQL <b>prima</b> della compressione, riga di chiusura esclusa.</param>
/// <param name="Sha256">Impronta degli stessi byte, esadecimale minuscolo.</param>
/// <param name="LongestStatementBytes">L'istruzione SQL più lunga, in byte: il <c>max_allowed_packet</c> che il
/// server di ripristino deve accettare (un KMZ da 8 MB diventa un <c>INSERT</c> da 16 MB).</param>
public sealed record DatabaseBackupSummary(int Tables, long Rows, long Bytes, string Sha256, long LongestStatementBytes);

/// <summary>L'ultima copia chiesta, per la scheda in Diagnostica.</summary>
/// <param name="RequestedUtc">Quando è stata chiesta.</param>
/// <param name="UserId">Chi l'ha chiesta (VID).</param>
/// <param name="Completed">Il riassunto, se è arrivata in fondo; null = interrotta, o ancora in corso.</param>
public sealed record DatabaseBackupLast(DateTime RequestedUtc, int UserId, DatabaseBackupSummary? Completed);

/// <summary>Una seconda copia chiesta mentre la prima non è finita.</summary>
public sealed class DatabaseBackupBusyException : Exception
{
    public DatabaseBackupBusyException() : base("C'è già una copia del database in corso.") { }
}

/// <summary>
/// La copia di sicurezza del database, scaricabile da un Admin (carta
/// <c>docs/feature/2026-09-16-copia-del-database.md</c>).
///
/// <para>⚠️ <b>Il cancello sta anche qui</b>, non solo nella pagina e nell'indirizzo: chi chiama
/// <see cref="WriteAsync"/> senza essere Admin riceve <c>EditNotAllowedException</c> prima che un solo byte
/// venga letto.</para>
/// </summary>
public interface IDatabaseBackup
{
    /// <summary>Vero se il database in uso sa fare la copia. Oggi solo MariaDB/MySQL, cioè la produzione:
    /// il file è un <c>.sql</c> che si reimporta lì.</summary>
    bool IsSupported { get; }

    /// <summary>Il nome proposto al browser, con la data e la versione del sito che l'ha scritta.</summary>
    string FileName(DateTime utc);

    /// <summary>
    /// Scrive la copia <b>compressa in gzip</b> su <paramref name="destination"/>. Tutto quel che può fallire
    /// prima di cominciare (connessione, elenco tabelle) fallisce <b>prima del primo byte</b>, così chi
    /// risponde a una richiesta HTTP può ancora dare un errore vero.
    /// </summary>
    /// <exception cref="DatabaseBackupBusyException">C'è già una copia in corso.</exception>
    Task<DatabaseBackupSummary> WriteAsync(Stream destination, CancellationToken ct = default);

    /// <summary>L'ultima copia chiesta, o null se non ce n'è mai stata una.</summary>
    Task<DatabaseBackupLast?> LastAsync(CancellationToken ct = default);
}
