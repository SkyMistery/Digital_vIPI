using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Vipi.DbSeed;
using Vipi.Infrastructure.Persistence;

// -----------------------------------------------------------------------------------------------
// Vipi.DbSeed — travasa i dati di un database vIPI in un altro (wipe + reseed), preservando gli ID.
//
//   dotnet run --project tools/Vipi.DbSeed -- --from-postgres "<conn>" --to-mysql "<conn>"
//
// Sorgenti: SQLite (il vipi.db di sviluppo) o Postgres (Neon, il deploy di prova).
// Destinazioni: Postgres o MySQL/MariaDB (atc.it.ivao.aero), oppure --dry-run per contare e basta.
//
// Il tool NON crea schema: la destinazione deve già averlo. Su Postgres lo riconcilia (EnsureCreated +
// colonne mancanti); su MariaDB lo schema nasce dalle migrazioni di Vipi.Infrastructure.MySqlMigrations,
// e qui ci si limita a verificare che ci sia — prima del wipe, che altrimenti lascerebbe la destinazione
// vuota e basta.
// -----------------------------------------------------------------------------------------------

if (!Argomenti.Prova(args, out var opzioni, out var errore))
{
    if (errore is not null) Console.Error.WriteLine($"Errore: {errore}\n");
    Console.Error.WriteLine(Argomenti.Uso);
    return 1;
}

// ---- 1) Sorgente ------------------------------------------------------------------------------

var srcBuilder = new DbContextOptionsBuilder<VipiDbContext>();
switch (opzioni.Sorgente)
{
    case Motore.Sqlite:
        if (!File.Exists(opzioni.SorgenteConnessione))
        {
            Console.Error.WriteLine($"File SQLite non trovato: {opzioni.SorgenteConnessione}");
            return 1;
        }
        srcBuilder.UseSqlite($"Data Source={opzioni.SorgenteConnessione}");
        break;

    case Motore.Postgres:
        srcBuilder.UseNpgsql(NormalizzaConnessionePostgres(opzioni.SorgenteConnessione));
        break;
}

using var src = new SeedDbContext(srcBuilder.Options);
src.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

// Tabelle in un ordine stabile (per output leggibile); l'ordine d'INSERT lo decide EF nel SaveChanges.
var entityTypes = src.Model.GetEntityTypes()
    .Where(e => e.GetTableName() is not null)
    .OrderBy(e => e.GetTableName())
    .ToList();

// Legge tutto (solo scalari + FK: con NoTracking non si carica nessuna navigation).
Console.WriteLine($"Lettura da {opzioni.Sorgente}: {Riassumi(opzioni.SorgenteConnessione)}");
var setMethod = typeof(DbContext).GetMethods()
    .Single(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

var allEntities = new List<object>();
var conteggioSorgente = new Dictionary<string, int>();
foreach (var et in entityTypes)
{
    var dbSet = setMethod.MakeGenericMethod(et.ClrType).Invoke(src, null)!;
    var rows = ((IEnumerable)dbSet).Cast<object>().ToList();
    NormalizeDateTimesToUtc(rows);
    allEntities.AddRange(rows);
    conteggioSorgente[et.GetTableName()!] = rows.Count;
    Console.WriteLine($"  {et.GetTableName(),-28} {rows.Count,6}");
}
Console.WriteLine($"Totale righe lette: {allEntities.Count}");

if (opzioni.Destinazione is null)
{
    Console.WriteLine("\n[--dry-run] Nessuna scrittura. Uscita.");
    return 0;
}

// ---- 2) Destinazione --------------------------------------------------------------------------

var mysql = opzioni.Destinazione == Motore.MySql;
var dstBuilder = new DbContextOptionsBuilder<VipiDbContext>();
if (mysql)
    dstBuilder.UseMySql(opzioni.DestinazioneConnessione!,
        MySqlSchema.ResolveServerVersion(opzioni.VersioneServer));
else
    dstBuilder.UseNpgsql(NormalizzaConnessionePostgres(opzioni.DestinazioneConnessione!));

using var dst = new SeedDbContext(dstBuilder.Options);

// Lo schema si garantisce PRIMA del wipe. Senza, una destinazione con una tabella o una colonna mancante
// supererebbe il TRUNCATE e fallirebbe l'INSERT: destinazione svuotata e non ricaricata.
Console.WriteLine("\nVerifica dello schema di destinazione…");
dst.Database.OpenConnection();

if (mysql)
{
    // Su MariaDB lo schema è prodotto dalle migrazioni, che non girano da qui: se manca, il tool si ferma
    // e dice quale comando lanciare. Riconciliare a mano come su Postgres non è un'opzione — la DDL di
    // MariaDB non è transazionale, quindi un reconcile interrotto lascerebbe lo schema a metà.
    var presenti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using (var cmd = dst.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE();";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) presenti.Add(reader.GetString(0));
    }

    var mancanti = entityTypes.Select(e => e.GetTableName()!).Where(t => !presenti.Contains(t)).ToList();
    if (mancanti.Count > 0)
    {
        Console.Error.WriteLine(
            $"Lo schema di destinazione è incompleto: mancano {mancanti.Count} tabelle " +
            $"({string.Join(", ", mancanti.Take(5))}{(mancanti.Count > 5 ? ", …" : "")}).\n" +
            "Applicare prima le migrazioni:\n" +
            "  dotnet ef database update --project src/Vipi.Infrastructure.MySqlMigrations " +
            "--startup-project src/Vipi.Infrastructure.MySqlMigrations --connection \"<conn>\"");
        return 1;
    }
    Console.WriteLine($"  {presenti.Count} tabelle presenti, nessuna mancante.");
}
else
{
    PostgresSchemaReconciler.InitializeSchema(dst);   // EnsureCreated + colonne e indici nuovi, sotto advisory lock
}

// ---- 3) Wipe ----------------------------------------------------------------------------------

var tabelle = entityTypes.Select(e => e.GetTableName()!).ToList();
Console.WriteLine($"\nTRUNCATE di {tabelle.Count} tabelle…");

if (mysql)
{
    // MariaDB non ha il CASCADE del TRUNCATE: con le FK attive un TRUNCATE su una tabella referenziata
    // fallisce, quindi si spengono i controlli per la durata del wipe e si tronca una tabella alla volta.
    // Si riaccendono SUBITO dopo: gli INSERT che seguono devono essere verificati davvero, o una FK rotta
    // nei dati di partenza entrerebbe in produzione senza che nessuno se ne accorga.
    dst.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 0;");
    foreach (var t in tabelle) dst.Database.ExecuteSqlRaw($"TRUNCATE TABLE {CitaMySql(t)};");
    dst.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 1;");
}
else
{
    dst.Database.ExecuteSqlRaw($"TRUNCATE {string.Join(", ", tabelle.Select(CitaPostgres))} RESTART IDENTITY CASCADE;");
}

// ---- 4) Inserimento ---------------------------------------------------------------------------

// Il ciclo Document↔DocumentVersion (Document.CurrentVersionId nullable → DocumentVersion,
// DocumentVersion.DocumentId → Document) va rotto a mano: EF con AddRange in blocco + AutoDetectChanges
// off NON lo spezza da solo (throw "circular dependency"). Fase 1: Document con CurrentVersionId=null;
// fase 2: si ripristinano i valori e si risalva. Non dipende dal provider.
Console.WriteLine("Inserimento…");
dst.ChangeTracker.AutoDetectChangesEnabled = false;

var docCurrentVersion = new List<(Vipi.Domain.Entities.Document Doc, int? VersionId)>();
foreach (var doc in allEntities.OfType<Vipi.Domain.Entities.Document>())
{
    docCurrentVersion.Add((doc, doc.CurrentVersionId));
    doc.CurrentVersionId = null;
}

dst.AddRange(allEntities);
var written = dst.SaveChanges();

foreach (var (doc, versionId) in docCurrentVersion)
    doc.CurrentVersionId = versionId;
dst.ChangeTracker.DetectChanges();   // AutoDetect è off: forzo il rilevamento delle modifiche.
written += dst.SaveChanges();
Console.WriteLine($"Righe inserite: {written}");

// ---- 5) Contatori identità --------------------------------------------------------------------

// Dopo insert con ID espliciti il prossimo valore generato deve superare il massimo, o il primo insert
// dell'applicazione collide con una riga appena travasata.
Console.WriteLine("Risincronizzo i contatori identità…");
if (mysql) RisincronizzaAutoIncrement(dst);
else RisincronizzaSequencePostgres(dst, entityTypes);

// ---- 6) Riconciliazione riga per riga ---------------------------------------------------------

// A occhio non si riconcilia: 4500 righe con una tabella vuota in mezzo hanno lo stesso aspetto di 4500
// righe giuste. Il confronto per tabella è l'unica lettura che dice davvero se il travaso è completo.
Console.WriteLine("\nRiconciliazione (sorgente → destinazione):");
var divergenze = 0;
foreach (var et in entityTypes)
{
    var tabella = et.GetTableName()!;
    var attese = conteggioSorgente[tabella];
    long trovate;
    using (var cmd = dst.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM {(mysql ? CitaMySql(tabella) : CitaPostgres(tabella))};";
        trovate = Convert.ToInt64(cmd.ExecuteScalar());
    }

    if (trovate != attese)
    {
        divergenze++;
        Console.WriteLine($"  ⚠ {tabella,-28} {attese,6} → {trovate,6}");
    }
}

if (divergenze > 0)
{
    Console.Error.WriteLine($"\n{divergenze} tabelle non combaciano. Il travaso NON è utilizzabile.");
    return 1;
}

Console.WriteLine($"  tutte le {entityTypes.Count} tabelle combaciano.");
Console.WriteLine("\nFatto. Travaso completato.");
return 0;

// ---- helper ------------------------------------------------------------------------------------

static string CitaPostgres(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

// I backtick non sono un vezzo: su Linux lower_case_table_names è 0, quindi il nome va scritto con le
// maiuscole esatte del modello, e citarlo evita che una tabella che collide con una parola riservata rompa.
static string CitaMySql(string identifier) => "`" + identifier.Replace("`", "``") + "`";

static string Riassumi(string connessione)
    => connessione.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
       connessione.Contains("pwd=", StringComparison.OrdinalIgnoreCase)
        ? "(connessione con credenziali)"
        : connessione;

/// <summary>
/// Porta l'AUTO_INCREMENT di ogni tabella oltre il massimo travasato. Le colonne interessate si chiedono a
/// <c>information_schema</c> invece di dedurle dal modello: le PK non identità (<c>StaffMember.UserId</c>,
/// <c>ImportState.Category</c>) non hanno un contatore, e un ALTER su quelle non avrebbe senso.
/// </summary>
static void RisincronizzaAutoIncrement(DbContext dst)
{
    var colonne = new List<(string Tabella, string Colonna)>();
    using (var cmd = dst.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = """
            SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND EXTRA LIKE '%auto_increment%';
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) colonne.Add((reader.GetString(0), reader.GetString(1)));
    }

    foreach (var (tabella, colonna) in colonne)
    {
        long prossimo;
        using (var cmd = dst.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = $"SELECT COALESCE(MAX(`{colonna}`), 0) + 1 FROM `{tabella}`;";
            prossimo = Convert.ToInt64(cmd.ExecuteScalar());
        }
        // AUTO_INCREMENT non accetta parametri: è un'opzione di tabella, non un valore. Il numero viene da
        // un COUNT/MAX sul database stesso, non dall'esterno.
        dst.Database.ExecuteSqlRaw($"ALTER TABLE `{tabella}` AUTO_INCREMENT = {prossimo};");
    }
    Console.WriteLine($"  {colonne.Count} contatori AUTO_INCREMENT riportati oltre il massimo.");
}

static void RisincronizzaSequencePostgres(DbContext dst, List<IEntityType> entityTypes)
{
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
        if (column is null) continue;   // proprietà non mappata su questa tabella: niente sequence da toccare
        var qt = CitaPostgres(table);
        var qc = CitaPostgres(column);
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
}

// Forza Kind=Utc su ogni DateTime/DateTime? (i nomi sono già *Utc semanticamente). Serve a due provider per
// due motivi: Npgsql rifiuta di scrivere un DateTime con Kind=Unspecified su timestamptz, e il DATETIME di
// MariaDB non porta fuso — se non si normalizza a monte, il fuso lo decide la macchina che gira il travaso.
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
static string NormalizzaConnessionePostgres(string raw)
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
