#if NET8_0
using System.Data;
using MySqlConnector;

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
public sealed class MySqlDumpSource : IDatabaseDumpSource
{
    /// <summary>
    /// Fuori dalla copia, di proposito. <c>DataProtectionKeys</c> sono le chiavi che firmano e cifrano i cookie di
    /// accesso: in un file che gira su un PC valgono solo come rischio, e al ripristino il sito ne crea di nuove
    /// (chi era dentro rientra).
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedTables = new[] { "DataProtectionKeys" };

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

            var creates = new List<(string Name, string Create)>();
            foreach (var t in tables)
            {
                await using var cmd = new MySqlCommand($"SHOW CREATE TABLE {SqlLiteral.Identifier(t)}", conn);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) throw new InvalidOperationException($"SHOW CREATE TABLE {t} non ha risposto.");
                creates.Add((t, r.GetString(1)));
            }

            string? lastMigration = null;
            if (tables.Contains("__EFMigrationsHistory"))
            {
                await using var cmd = new MySqlCommand(
                    "SELECT MigrationId FROM `__EFMigrationsHistory` ORDER BY MigrationId DESC LIMIT 1", conn);
                lastMigration = await cmd.ExecuteScalarAsync(ct) as string;
            }

            return new Snapshot(conn, conn.ServerVersion, lastMigration, excluded, creates);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private static async Task ExecAsync(MySqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private sealed class Snapshot : IDumpSnapshot
    {
        private readonly MySqlConnection _conn;
        private readonly IReadOnlyList<(string Name, string Create)> _tables;

        public Snapshot(MySqlConnection conn, string serverVersion, string? lastMigration,
            IReadOnlyList<string> excluded, IReadOnlyList<(string Name, string Create)> tables)
        {
            _conn = conn;
            ServerVersion = serverVersion;
            LastMigration = lastMigration;
            Excluded = excluded;
            _tables = tables;
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
        }

        public async ValueTask DisposeAsync()
        {
            // Sola lettura: chiudere la connessione chiude anche la transazione. Nessun COMMIT da fare.
            await _conn.DisposeAsync();
        }
    }
}
#endif
