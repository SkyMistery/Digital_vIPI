using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Allinea lo schema Postgres al modello EF quando si usa <c>EnsureCreated</c> (deploy Render+Neon + tool DbSeed):
/// <c>EnsureCreated</c> crea le tabelle mancanti ma NON aggiunge colonne a tabelle già esistenti, così ogni colonna nuova
/// del modello manda il DB in drift (es. <c>42703 column ... does not exist</c>). Per ogni colonna del modello relazionale
/// assente nella tabella reale emette un <c>ADD COLUMN IF NOT EXISTS</c> col tipo store del modello; le NOT NULL ricevono un
/// default sicuro per non violare le righe esistenti, poi il default viene rimosso (fedeltà al modello). Idempotente e
/// best-effort: un errore su una colonna non blocca il chiamante. No-op sui provider non-Npgsql. Vedi ADR-0007.
/// </summary>
public static class PostgresSchemaReconciler
{
    public static void EnsureModelColumns(DbContext db)
    {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true) return;
        try
        {
            var conn = db.Database.GetDbConnection();
            var mustClose = conn.State != ConnectionState.Open;
            if (mustClose) conn.Open();
            try
            {
                // Colonne reali dello schema public: (tabella, colonna) case-sensitive come le crea EF.
                var actual = new HashSet<(string Table, string Column)>();
                using (var read = conn.CreateCommand())
                {
                    read.CommandText = "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = 'public'";
                    using var r = read.ExecuteReader();
                    while (r.Read()) actual.Add((r.GetString(0), r.GetString(1)));
                }

                foreach (var table in db.Model.GetRelationalModel().Tables)
                {
                    if (!string.IsNullOrEmpty(table.Schema) && !string.Equals(table.Schema, "public", StringComparison.Ordinal))
                        continue;   // reconcile solo lo schema public
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
                            Console.Error.WriteLine($"[vIPI] reconcile colonna {table.Name}.{col.Name} fallito: {ex.Message}");
                        }
                    }
                }
            }
            finally { if (mustClose) conn.Close(); }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[vIPI] reconcile schema Postgres fallito (chiamante prosegue): {ex.Message}");
        }
    }

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
}
