using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Vipi.DbSeed;
using Vipi.Infrastructure.Persistence;

// -----------------------------------------------------------------------------------------------
// Vipi.DbSeed — copia i dati del DB SQLite locale nel Postgres hostato (wipe + reseed).
//
//   dotnet run --project tools/Vipi.DbSeed -- <percorso-vipi.db> "<connstring-postgres>"
//
// La destinazione DEVE avere già lo schema (lo crea EnsureCreated all'avvio del deploy). Questo
// tool NON crea schema: TRUNCATE di tutte le tabelle, poi reinserisce preservando gli ID, infine
// risincronizza le sequence identità. La connstring Postgres è in formato keyword Npgsql
// (Host=...;Database=...;Username=...;Password=...;SSL Mode=Require) oppure URL postgres://.
// -----------------------------------------------------------------------------------------------

if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: dotnet run --project tools/Vipi.DbSeed -- <vipi.db> \"<connstring-postgres>\"");
    return 1;
}

var sqlitePath = args[0];
var dryRun = string.Equals(args[1], "--dry-run", StringComparison.OrdinalIgnoreCase);
var pgConn = dryRun ? "" : NormalizePgConnString(args[1]);

if (!File.Exists(sqlitePath))
{
    Console.Error.WriteLine($"File SQLite non trovato: {sqlitePath}");
    return 1;
}

var srcOptions = new DbContextOptionsBuilder<VipiDbContext>()
    .UseSqlite($"Data Source={sqlitePath}")
    .Options;
using var src = new SeedDbContext(srcOptions);
src.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

// Tabelle in un ordine stabile (per output leggibile); l'ordine d'INSERT lo decide EF nel SaveChanges.
var entityTypes = src.Model.GetEntityTypes()
    .Where(e => e.GetTableName() is not null)
    .OrderBy(e => e.GetTableName())
    .ToList();

// 1) Leggi tutto dalla sorgente SQLite (solo scalari + FK: nessuna navigation caricata con NoTracking).
Console.WriteLine($"Lettura da SQLite: {sqlitePath}");
var setMethod = typeof(DbContext).GetMethods()
    .Single(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

var allEntities = new List<object>();
foreach (var et in entityTypes)
{
    var dbSet = setMethod.MakeGenericMethod(et.ClrType).Invoke(src, null)!;
    var rows = ((IEnumerable)dbSet).Cast<object>().ToList();
    NormalizeDateTimesToUtc(rows);   // Npgsql (timestamptz) pretende Kind=Utc; SQLite li rilegge Unspecified.
    allEntities.AddRange(rows);
    Console.WriteLine($"  {et.GetTableName(),-28} {rows.Count,6}");
}
Console.WriteLine($"Totale righe lette: {allEntities.Count}");

if (dryRun)
{
    Console.WriteLine("\n[--dry-run] Nessuna scrittura su Postgres. Uscita.");
    return 0;
}

using var dst = new SeedDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseNpgsql(pgConn).Options);

// Schema: garantiscilo e allinealo al modello PRIMA del wipe. Senza questo, se il DB avesse una colonna nuova mancante
// (drift EnsureCreated), il TRUNCATE passerebbe ma l'INSERT fallirebbe → destinazione lasciata VUOTA. Reconcile evita il rischio.
Console.WriteLine("Verifica/allineamento schema di destinazione…");
dst.Database.EnsureCreated();
PostgresSchemaReconciler.EnsureModelColumns(dst);

// 2) Wipe della destinazione: TRUNCATE di tutte le tabelle in un colpo (CASCADE per le FK).
var quotedTables = entityTypes.Select(e => Quote(e.GetTableName()!)).ToList();
Console.WriteLine($"\nConnessione a Postgres e TRUNCATE di {quotedTables.Count} tabelle…");
dst.Database.OpenConnection();
dst.Database.ExecuteSqlRaw($"TRUNCATE {string.Join(", ", quotedTables)} RESTART IDENTITY CASCADE;");

// 3) Reinserisci tutto. Il ciclo Document↔DocumentVersion (Document.CurrentVersionId nullable →
//    DocumentVersion, DocumentVersion.DocumentId → Document) va rotto a mano: EF con AddRange in
//    blocco + AutoDetectChanges off NON lo spezza da solo (throw "circular dependency"). Fase 1:
//    inserisco i Document con CurrentVersionId=null; Fase 2: ripristino i valori e risalvo.
Console.WriteLine("Inserimento…");
dst.ChangeTracker.AutoDetectChangesEnabled = false;

// Stash + azzera CurrentVersionId sui Document, così l'INSERT non ha edge circolari.
var docCurrentVersion = new List<(Vipi.Domain.Entities.Document Doc, int? VersionId)>();
foreach (var doc in allEntities.OfType<Vipi.Domain.Entities.Document>())
{
    docCurrentVersion.Add((doc, doc.CurrentVersionId));
    doc.CurrentVersionId = null;
}

dst.AddRange(allEntities);
var written = dst.SaveChanges();

// Fase 2: rimetti i CurrentVersionId ora che le DocumentVersion esistono.
foreach (var (doc, versionId) in docCurrentVersion)
    doc.CurrentVersionId = versionId;
dst.ChangeTracker.DetectChanges();   // AutoDetect è off: forzo il rilevamento delle modifiche.
written += dst.SaveChanges();
Console.WriteLine($"Righe inserite: {written}");

// 4) Risincronizza le sequence identità: dopo insert con ID espliciti, next-val deve superare il max.
Console.WriteLine("Risincronizzo le sequence identità…");
foreach (var et in entityTypes)
{
    var pk = et.FindPrimaryKey();
    if (pk is null || pk.Properties.Count != 1) continue;
    var keyProp = pk.Properties[0];
    var t = Nullable.GetUnderlyingType(keyProp.ClrType) ?? keyProp.ClrType;
    if (t != typeof(int) && t != typeof(long)) continue;   // solo PK numeriche identità

    var sid = StoreObjectIdentifier.Create(et, StoreObjectType.Table)!.Value;
    var table = et.GetTableName()!;
    var column = keyProp.GetColumnName(sid);
    var qt = Quote(table);
    var qc = Quote(column);
    // pg_get_serial_sequence è null per PK non-identità (StaffMember.UserId, ImportState.Category): no-op.
    dst.Database.ExecuteSqlRaw($@"
DO $$
DECLARE s text;
BEGIN
  s := pg_get_serial_sequence('{qt}', '{column.Replace("'", "''")}');
  IF s IS NOT NULL THEN
    PERFORM setval(s, GREATEST((SELECT COALESCE(MAX({qc}), 0) FROM {qt}), 1), (SELECT COUNT(*) > 0 FROM {qt}));
  END IF;
END $$;");
}

Console.WriteLine("\nFatto. Seed completato.");
return 0;

// ---- helper ------------------------------------------------------------------------------------

static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

// Forza Kind=Utc su ogni DateTime/DateTime? (i nomi sono già *Utc semanticamente). Npgsql 8 rifiuta
// di scrivere un DateTime con Kind=Unspecified su colonna timestamptz; SQLite li rilegge Unspecified.
static void NormalizeDateTimesToUtc(IEnumerable<object> rows)
{
    foreach (var row in rows)
    {
        var props = row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in props)
        {
            if (!p.CanRead || !p.CanWrite) continue;
            if (p.PropertyType == typeof(DateTime))
            {
                var v = (DateTime)p.GetValue(row)!;
                if (v.Kind != DateTimeKind.Utc) p.SetValue(row, DateTime.SpecifyKind(v, DateTimeKind.Utc));
            }
            else if (p.PropertyType == typeof(DateTime?))
            {
                var v = (DateTime?)p.GetValue(row);
                if (v.HasValue && v.Value.Kind != DateTimeKind.Utc)
                    p.SetValue(row, DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
            }
        }
    }
}

// Accetta sia il formato keyword Npgsql sia un URL postgres://user:pass@host:port/db (comodo copincolla
// da Neon). Aggiunge SSL Mode=Require (Neon lo esige) se non già presente.
static string NormalizePgConnString(string raw)
{
    raw = raw.Trim();
    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var db = uri.AbsolutePath.TrimStart('/');
        var parts = new List<string>
        {
            $"Host={uri.Host}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Database={db}",
            $"Username={Uri.UnescapeDataString(userInfo[0])}",
        };
        if (userInfo.Length > 1) parts.Add($"Password={Uri.UnescapeDataString(userInfo[1])}");
        parts.Add("SSL Mode=Require");
        parts.Add("Trust Server Certificate=true");
        return string.Join(";", parts);
    }
    if (!raw.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase) &&
        !raw.Contains("SslMode", StringComparison.OrdinalIgnoreCase))
        raw = raw.TrimEnd(';') + ";SSL Mode=Require;Trust Server Certificate=true";
    return raw;
}
