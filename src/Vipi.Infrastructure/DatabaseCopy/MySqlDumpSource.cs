using System.Data;
using System.Text.RegularExpressions;
using MySqlConnector;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.DatabaseCopy;

/// <summary>
/// Legge MariaDB per la copia di sicurezza, con una connessione <b>sua</b> e una transazione sola.
///
/// <para>⚠️ <b>Non il DbContext.</b> La copia dura secondi, legge tutto, e tiene aperta una transazione di sola
/// lettura: sul contesto della richiesta si porterebbe dietro il retry di EF (che rifarebbe una copia a metà già
/// spedita) e il tracciamento. Qui c'è solo MySqlConnector, la stessa libreria che Pomelo usa sotto.</para>
///
/// <para>⚠️ <b>La connessione chiede i valori crudi</b>: <c>GuidFormat=None</c> (un <c>CHAR(36)</c> torna la
/// stringa com'è salvata, non un <c>Guid</c> riformattato in minuscolo) e <c>TreatTinyAsBoolean=false</c> (un
/// <c>TINYINT(1)</c> torna il numero). Quel che si rilegge dev'essere quel che si reinserisce, byte per byte.</para>
/// </summary>
public sealed partial class MySqlDumpSource : IDatabaseDumpSource
{
    /// <summary>
    /// Fuori dalla copia, di proposito. <c>DataProtectionKeys</c> sono le chiavi che firmano e cifrano i cookie di
    /// accesso: in un file che gira su un PC valgono solo come rischio, e al ripristino il sito ne crea di nuove
    /// (chi era dentro rientra).
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedTables = new[] { "DataProtectionKeys" };

    /// <summary>
    /// I tipi di colonna che <see cref="SqlLiteral"/> sa riscrivere fedeli. Un tipo fuori elenco ferma la copia
    /// <b>all'apertura</b>, col nome della colonna nel messaggio: scoprirlo a metà flusso vorrebbe dire un download
    /// già partito e un «download non riuscito» senza spiegazione nel browser.
    /// </summary>
    internal static readonly IReadOnlySet<string> TipiNoti = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "tinyint", "smallint", "mediumint", "int", "bigint", "decimal", "double", "float",
        "char", "varchar", "tinytext", "text", "mediumtext", "longtext",
        "binary", "varbinary", "tinyblob", "blob", "mediumblob", "longblob",
        "date", "datetime", "timestamp", "time",
    };

    private readonly string _connectionString;

    public MySqlDumpSource(string connectionString) => _connectionString = connectionString;

    public async Task<IDumpSnapshot> OpenAsync(CancellationToken ct = default)
    {
        var csb = new MySqlConnectionStringBuilder(_connectionString)
        {
            GuidFormat = MySqlGuidFormat.None,
            TreatTinyAsBoolean = false,
            CharacterSet = "utf8mb4",
            // Una tabella grossa si legge in un comando solo: il default di 30 s è pensato per una pagina.
            DefaultCommandTimeout = 600,
            // Niente pool condiviso con il sito: questa connessione esce dalla copia con una transazione chiusa
            // e impostazioni di sessione cambiate, e non deve tornare in giro.
            Pooling = false,
        };

        var conn = new MySqlConnection(csb.ConnectionString);
        try
        {
            await conn.OpenAsync(ct);
            await ExecAsync(conn, "SET SESSION time_zone = '+00:00'", ct);
            // Il server scrive le righe a NOI, e noi le giriamo a un browser: se il browser rallenta, noi smettiamo
            // di leggere e il server aspetta. Col default di 60 s una rete lenta chiuderebbe la lettura a metà.
            await ExecAsync(conn, "SET SESSION net_write_timeout = 600, net_read_timeout = 600", ct);
            await ExecAsync(conn, "SET SESSION TRANSACTION ISOLATION LEVEL REPEATABLE READ", ct);
            // Da qui ogni SELECT vede il database com'era in QUESTO istante, anche se qualcuno salva durante la copia.
            await ExecAsync(conn, "START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY", ct);

            var tables = new List<string>();
            await using (var cmd = new MySqlCommand(
                "SELECT TABLE_NAME FROM information_schema.TABLES " +
                "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME", conn))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) tables.Add(r.GetString(0));

            var excluded = tables.Where(t => ExcludedTables.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
            tables.RemoveAll(t => excluded.Contains(t));
            if (tables.Count == 0)
                throw new InvalidOperationException("Il database non ha tabelle: non c'è niente da copiare.");

            await ControllaCheSiPossaCopiareAsync(conn, excluded, ct);

            var creates = new List<(string Name, string Create)>();
            foreach (var t in tables)
            {
                await using var cmd = new MySqlCommand($"SHOW CREATE TABLE {SqlLiteral.Identifier(t)}", conn);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) throw new InvalidOperationException($"SHOW CREATE TABLE {t} non ha risposto.");
                creates.Add((t, r.GetString(1)));
            }

            // Le viste condivise (le altre le ha già fermate il controllo): la loro CREATE, senza DEFINER.
            var views = new List<(string Name, string Create)>();
            foreach (var v in await ViewNamesAsync(conn, ct))
            {
                if (!IsSharedView(v)) continue;
                await using var cmd = new MySqlCommand($"SHOW CREATE VIEW {SqlLiteral.Identifier(v)}", conn);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) throw new InvalidOperationException($"SHOW CREATE VIEW {v} non ha risposto.");
                views.Add((v, WithoutDefiner(r.GetString(1))));
            }

            string? lastMigration = null;
            if (tables.Contains("__EFMigrationsHistory"))
            {
                await using var cmd = new MySqlCommand(
                    "SELECT MigrationId FROM `__EFMigrationsHistory` ORDER BY MigrationId DESC LIMIT 1", conn);
                lastMigration = await cmd.ExecuteScalarAsync(ct) as string;
            }

            return new Snapshot(conn, conn.ServerVersion, lastMigration, excluded, creates, views);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Quello che la copia non saprebbe riportare indietro fedele, cercato PRIMA di scrivere un byte: tipi di colonna
    /// fuori elenco, colonne generate o invisibili (un INSERT con tutte le colonne fallirebbe al ripristino), e
    /// viste, trigger, procedure (la copia porta solo tabelle). Oggi lo schema non ne ha: se un giorno ne avrà,
    /// la copia si rifiuta e dice perché, invece di produrre un file che non torna.
    /// <para>L'eccezione sono le viste condivise (<see cref="MySqlSchema.SharedViewPrefix"/>, dal 23 settembre
    /// 2026): le crea una migrazione, e un ripristino che le perdesse lascerebbe la storia delle migrazioni a dire
    /// «fatto» su una vista che non c'è — e l'hub senza dati. Quelle la copia le porta.</para>
    /// </summary>
    private static async Task ControllaCheSiPossaCopiareAsync(MySqlConnection conn, IReadOnlyList<string> escluse, CancellationToken ct)
    {
        var problemi = new List<string>();
        await using (var cmd = new MySqlCommand(
            "SELECT c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.EXTRA FROM information_schema.COLUMNS c " +
            "JOIN information_schema.TABLES t ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME " +
            "WHERE c.TABLE_SCHEMA = DATABASE() AND t.TABLE_TYPE = 'BASE TABLE'", conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var tabella = r.GetString(0);
                if (escluse.Contains(tabella, StringComparer.OrdinalIgnoreCase)) continue;
                var dove = $"{tabella}.{r.GetString(1)}";
                var tipo = r.GetString(2);
                var extra = r.IsDBNull(3) ? "" : r.GetString(3);
                if (!TipiNoti.Contains(tipo)) problemi.Add($"{dove} è di tipo {tipo}");
                // ⚠️ Non «GENERATED» nudo: MySQL scrive DEFAULT_GENERATED su un semplice DEFAULT CURRENT_TIMESTAMP,
                // che si copia benissimo. MariaDB scrive «VIRTUAL GENERATED» / «STORED GENERATED» / «INVISIBLE».
                if (extra.Contains("VIRTUAL GENERATED", StringComparison.OrdinalIgnoreCase) ||
                    extra.Contains("STORED GENERATED", StringComparison.OrdinalIgnoreCase) ||
                    extra.Contains("PERSISTENT", StringComparison.OrdinalIgnoreCase) ||
                    extra.Contains("INVISIBLE", StringComparison.OrdinalIgnoreCase))
                    problemi.Add($"{dove} è una colonna {extra}");
            }
        }

        foreach (var v in await ViewNamesAsync(conn, ct))
            if (!IsSharedView(v)) problemi.Add($"vista {v}");

        foreach (var (vista, cosa) in new[]
        {
            ("SELECT TRIGGER_NAME FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE()", "trigger"),
            ("SELECT ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE()", "procedura"),
        })
        {
            await using var cmd = new MySqlCommand(vista, conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) problemi.Add($"{cosa} {r.GetString(0)}");
        }

        if (problemi.Count > 0)
            throw new NotSupportedException(
                "La copia non parte: lo schema contiene cose che non saprebbe riportare indietro fedeli — " +
                string.Join("; ", problemi.Take(10)) + (problemi.Count > 10 ? $" (e altre {problemi.Count - 10})" : "") + ".");
    }

    private static async Task<List<string>> ViewNamesAsync(MySqlConnection conn, CancellationToken ct)
    {
        var nomi = new List<string>();
        await using var cmd = new MySqlCommand(
            "SELECT TABLE_NAME FROM information_schema.VIEWS WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) nomi.Add(r.GetString(0));
        return nomi;
    }

    /// <summary>Una vista che la copia sa portare: solo quelle condivise, per prefisso esatto.</summary>
    internal static bool IsSharedView(string name) =>
        name.StartsWith(MySqlSchema.SharedViewPrefix, StringComparison.Ordinal);

    /// <summary>
    /// La <c>CREATE</c> di <c>SHOW CREATE VIEW</c> senza la clausola <c>DEFINER=`utente`@`host`</c>.
    ///
    /// <para>⚠️ <b>Senza DEFINER, e non per pulizia.</b> Il file si ripristina altrove, con un altro utente: una
    /// <c>CREATE VIEW</c> che nomina un definer diverso da chi la esegue chiede un privilegio (<c>SET USER</c>) che
    /// l'utente di un database ospitato non ha, e se il definer non esiste la vista nasce ma non si legge. Tolta la
    /// clausola, il definer è chi ripristina — lo stesso utente che poi il sito usa. <c>SQL SECURITY DEFINER</c>
    /// resta com'è. Il nome del database non c'è da togliere: <c>SHOW CREATE VIEW</c> nel database corrente scrive
    /// le tabelle senza qualificarle (verificato su MariaDB 11.4.10, e lo riprova l'andata e ritorno della CI).</para>
    /// </summary>
    internal static string WithoutDefiner(string create) => Definer().Replace(create, " ", 1);

    [GeneratedRegex(@"\s+DEFINER=`(?:[^`]|``)*`@`(?:[^`]|``)*`\s+")]
    private static partial Regex Definer();

    private static async Task ExecAsync(MySqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private sealed class Snapshot : IDumpSnapshot
    {
        private readonly MySqlConnection _conn;
        private readonly IReadOnlyList<(string Name, string Create)> _tables;
        private readonly IReadOnlyList<(string Name, string Create)> _views;

        public Snapshot(MySqlConnection conn, string serverVersion, string? lastMigration,
            IReadOnlyList<string> excluded, IReadOnlyList<(string Name, string Create)> tables,
            IReadOnlyList<(string Name, string Create)> views)
        {
            _conn = conn;
            ServerVersion = serverVersion;
            LastMigration = lastMigration;
            Excluded = excluded;
            _tables = tables;
            _views = views;
        }

        public string ServerVersion { get; }
        public string? LastMigration { get; }
        public IReadOnlyList<string> Excluded { get; }

        public async Task WriteTablesAsync(SqlDumpWriter writer, CancellationToken ct = default)
        {
            foreach (var (name, create) in _tables)
            {
                await using var cmd = new MySqlCommand($"SELECT * FROM {SqlLiteral.Identifier(name)}", _conn);
                // SequentialAccess: una riga con un'immagine dentro non si tiene due volte in memoria.
                await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

                var columns = Enumerable.Range(0, r.FieldCount).Select(r.GetName).ToArray();
                await writer.BeginTableAsync(name, create, columns, ct);

                var values = new object?[r.FieldCount];
                while (await r.ReadAsync(ct))
                {
                    for (var i = 0; i < values.Length; i++)
                        values[i] = await r.IsDBNullAsync(i, ct) ? null : r.GetValue(i);
                    await writer.WriteRowAsync(values, ct);
                }
                await writer.EndTableAsync(ct);
            }

            // Le viste DOPO le tabelle: la CREATE VIEW controlla che le tabelle che legge esistano già.
            foreach (var (name, create) in _views)
                await writer.WriteViewAsync(name, create, ct);
        }

        public async ValueTask DisposeAsync()
        {
            // Sola lettura: chiudere la connessione chiude anche la transazione. Nessun COMMIT da fare.
            await _conn.DisposeAsync();
        }
    }
}
