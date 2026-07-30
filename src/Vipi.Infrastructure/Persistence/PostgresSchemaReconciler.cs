using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Crea e allinea lo schema Postgres quando si usa <c>EnsureCreated</c> (deploy Render+Neon + tool DbSeed):
/// <c>EnsureCreated</c> crea le tabelle mancanti ma NON tocca le tabelle già esistenti, così ogni colonna o indice
/// aggiunto al modello dopo il primo deploy manda il DB in drift (es. <c>42703 column ... does not exist</c>).
/// Per ogni colonna del modello assente emette un <c>ADD COLUMN IF NOT EXISTS</c> col tipo store del modello (le NOT
/// NULL ricevono un default sicuro per non violare le righe esistenti, poi il default viene rimosso); per ogni indice
/// del modello assente emette un <c>CREATE INDEX IF NOT EXISTS</c>. Idempotente e best-effort: un errore su un
/// singolo oggetto non blocca il chiamante. No-op sui provider non-Npgsql. Vedi ADR-0007.
/// </summary>
public static class PostgresSchemaReconciler
{
    /// <summary>
    /// Chiave dell'advisory lock che serializza l'inizializzazione dello schema fra processi ("vIPI" in ASCII).
    /// Arbitraria ma stabile: conta solo che tutte le istanze usino la stessa.
    /// </summary>
    private const long SchemaLockKey = 0x76495049;

    /// <summary>
    /// Punto d'ingresso unico: crea lo schema e lo allinea al modello, serializzato fra processi.
    /// <para>
    /// L'advisory lock serve perché un rolling deploy su Render fa coesistere l'istanza vecchia e quella nuova (e
    /// più repliche avvierebbero insieme): due <c>EnsureCreated</c> concorrenti su un DB vuoto possono collidere con
    /// un errore di tabella duplicata, che qui non era intercettato e abbatteva l'avvio dell'istanza perdente.
    /// È un lock di sessione tenuto sulla connessione aperta qui, quindi copre anche il reconcile che segue; se il
    /// processo muore, Postgres lo rilascia alla caduta della connessione.
    /// </para>
    /// </summary>
    public static void InitializeSchema(DbContext db, ILogger? log = null)
    {
        if (!IsNpgsql(db)) return;

        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        try
        {
            if (mustClose) conn.Open();

            var locked = TryExec(conn, $"SELECT pg_advisory_lock({SchemaLockKey})", log, "acquisizione advisory lock");
            try
            {
                db.Database.EnsureCreated();
                EnsureModelColumns(db, conn, log);
                EnsureModelIndexes(db, conn, log);
            }
            finally
            {
                if (locked) TryExec(conn, $"SELECT pg_advisory_unlock({SchemaLockKey})", log, "rilascio advisory lock");
            }
        }
        catch (Exception ex)
        {
            Warn(log, "init schema Postgres fallita (il chiamante prosegue): {Message}", ex.Message);
        }
        finally
        {
            if (mustClose && conn.State == ConnectionState.Open) conn.Close();
        }
    }

    private static bool IsNpgsql(DbContext db) =>
        db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    // --- Colonne ---

    private static void EnsureModelColumns(DbContext db, IDbConnection conn, ILogger? log)
    {
        // Colonne reali dello schema public: (tabella, colonna) case-sensitive come le crea EF.
        var actual = new HashSet<(string Table, string Column)>();
        using (var read = conn.CreateCommand())
        {
            read.CommandText = "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = 'public'";
            using var r = read.ExecuteReader();
            while (r.Read()) actual.Add((r.GetString(0), r.GetString(1)));
        }

        foreach (var table in PublicTables(db))
        {
            foreach (var col in table.Columns)
            {
                if (actual.Contains((table.Name, col.Name))) continue;   // già presente
                try
                {
#pragma warning disable EF1002 // identificatori dal modello EF (tabella/colonna/tipo store), non input utente → nessuna SQL injection
                    if (col.IsNullable)
                    {
                        db.Database.ExecuteSqlRaw($"ALTER TABLE \"{table.Name}\" ADD COLUMN IF NOT EXISTS \"{col.Name}\" {col.StoreType} NULL");
                    }
                    else
                    {
                        // NOT NULL su tabella con righe: aggiungi con default sicuro (backfilla le righe), poi togli il default.
                        db.Database.ExecuteSqlRaw($"ALTER TABLE \"{table.Name}\" ADD COLUMN IF NOT EXISTS \"{col.Name}\" {col.StoreType} NOT NULL DEFAULT {DefaultLiteral(col.StoreType)}");
                        db.Database.ExecuteSqlRaw($"ALTER TABLE \"{table.Name}\" ALTER COLUMN \"{col.Name}\" DROP DEFAULT");
                    }
#pragma warning restore EF1002
                }
                catch (Exception ex)
                {
                    Warn(log, "reconcile colonna {Table}.{Column} fallito: {Message}", table.Name, col.Name, ex.Message);
                }
            }
        }
    }

    // --- Indici ---

    private static void EnsureModelIndexes(DbContext db, IDbConnection conn, ILogger? log)
    {
        var actual = new HashSet<string>(StringComparer.Ordinal);
        using (var read = conn.CreateCommand())
        {
            read.CommandText = "SELECT indexname FROM pg_indexes WHERE schemaname = 'public'";
            using var r = read.ExecuteReader();
            while (r.Read()) actual.Add(r.GetString(0));
        }

        foreach (var table in PublicTables(db))
        {
            foreach (var ix in table.Indexes)
            {
                if (ix.Name is null || actual.Contains(ix.Name)) continue;
                var cols = string.Join(", ", ix.Columns.Select(c => $"\"{c.Name}\""));
                var unique = ix.IsUnique ? "UNIQUE " : "";
                try
                {
#pragma warning disable EF1002 // identificatori dal modello EF, non input utente
                    db.Database.ExecuteSqlRaw($"CREATE {unique}INDEX IF NOT EXISTS \"{ix.Name}\" ON \"{table.Name}\" ({cols})");
#pragma warning restore EF1002
                }
                catch (Exception ex)
                {
                    // Tipico: indice UNIQUE su una tabella che contiene già duplicati. Va segnalato, non fatto passare
                    // in silenzio, ma non deve impedire l'avvio dell'applicazione.
                    Warn(log, "reconcile indice {Index} su {Table} fallito: {Message}", ix.Name, table.Name, ex.Message);
                }
            }
        }
    }

    private static IEnumerable<Microsoft.EntityFrameworkCore.Metadata.ITable> PublicTables(DbContext db) =>
        db.Model.GetRelationalModel().Tables
            .Where(t => string.IsNullOrEmpty(t.Schema) || string.Equals(t.Schema, "public", StringComparison.Ordinal));

    // Default sicuro per una colonna NOT NULL aggiunta a una tabella con righe esistenti, in base al tipo store Postgres.
    private static string DefaultLiteral(string storeType)
    {
        var t = storeType.ToLowerInvariant();
        if (t.StartsWith("bool")) return "false";
        if (t.StartsWith("smallint") || t.StartsWith("int") || t.StartsWith("bigint") || t.Contains("serial")) return "0";
        if (t.StartsWith("numeric") || t.StartsWith("decimal") || t.StartsWith("real") || t.StartsWith("double") || t.StartsWith("money")) return "0";
        if (t.StartsWith("timestamp")) return "now()";
        if (t.StartsWith("date")) return "CURRENT_DATE";
        if (t.StartsWith("time")) return "CURRENT_TIME";
        if (t.StartsWith("uuid")) return "'00000000-0000-0000-0000-000000000000'";
        if (t.StartsWith("bytea")) return "''::bytea";
        if (t.StartsWith("json")) return "'{}'";
        return "''";   // text/varchar/char/citext/name e fallback: stringa vuota
    }

    private static bool TryExec(IDbConnection conn, string sql, ILogger? log, string what)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteScalar();
            return true;
        }
        catch (Exception ex)
        {
            // Senza lock si prosegue comunque: la DDL sotto è idempotente (IF NOT EXISTS) e best-effort.
            Warn(log, "{What} fallito: {Message}", what, ex.Message);
            return false;
        }
    }

    // Il tool DbSeed gira senza host, quindi senza ILogger: in quel caso resta stderr. Nell'app il logger c'è, e
    // usarlo è indispensabile perché su Render lo stderr grezzo non finisce nei log strutturati.
    private static void Warn(ILogger? log, string message, params object?[] args)
    {
        if (log is not null) log.LogWarning("[vIPI schema] " + message, args);
        else Console.Error.WriteLine("[vIPI schema] " + string.Format(Templatize(message), args));
    }

    private static string Templatize(string message)
    {
        // "{Table}.{Column} ..." → "{0}.{1} ..." per string.Format nel fallback senza logger.
        var i = 0;
        return System.Text.RegularExpressions.Regex.Replace(message, @"\{\w+\}", _ => "{" + i++ + "}");
    }
}
