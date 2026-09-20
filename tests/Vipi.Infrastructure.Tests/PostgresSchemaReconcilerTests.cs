using Microsoft.EntityFrameworkCore;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// In produzione lo schema non nasce da migrazioni ma da <c>EnsureCreated</c> + riconciliazione (ADR-0007), e
/// <c>EnsureCreated</c> non tocca un database che ha già tabelle: senza questo passo un'entità NUOVA esisterebbe in
/// locale (SQLite, <c>Migrate()</c>) e non su Neon, e il primo uso morirebbe con <c>42P01 relation does not exist</c>.
/// Qui si verifica la generazione della DDL, che è pura: il modello Npgsql si costruisce senza un Postgres vivo
/// (stesso trucco di <see cref="SchemaDriftStoreTypesTests"/>).
/// </summary>
public class PostgresSchemaReconcilerTests
{
    private static VipiDbContext NpgsqlModel() => new(
        new DbContextOptionsBuilder<VipiDbContext>()
            .UseNpgsql("Host=nowhere;Database=x;Username=u;Password=p")
            .Options);

    private static IReadOnlyList<string> TablesOf(DbContext db) =>
        db.Model.GetRelationalModel().Tables.Select(t => t.Name).ToList();

    [Fact]
    public void Database_allineato_al_modello_non_genera_ddl()
    {
        using var db = NpgsqlModel();

        var sql = PostgresSchemaReconciler.CreateTableStatements(db, TablesOf(db).ToHashSet(StringComparer.Ordinal));

        Assert.Empty(sql);
    }

    [Fact]
    public void Tabella_assente_viene_creata_e_le_altre_no()
    {
        using var db = NpgsqlModel();
        var tutte = TablesOf(db);
        var mancante = tutte.First(t => t == "Documents");
        var presenti = tutte.Where(t => t != mancante).ToHashSet(StringComparer.Ordinal);

        var sql = PostgresSchemaReconciler.CreateTableStatements(db, presenti);

        var unica = Assert.Single(sql);
        Assert.Contains($"CREATE TABLE \"{mancante}\"", unica);
        // La DDL viene da EF, quindi si porta dietro la chiave primaria (ed è ciò che la distingue da un CREATE
        // TABLE scritto a mano: nessun elenco di colonne da tenere aggiornato).
        Assert.Contains("PRIMARY KEY", unica);
    }

    [Fact]
    public void Database_vuoto_genera_una_create_per_ogni_tabella_del_modello()
    {
        using var db = NpgsqlModel();

        var sql = PostgresSchemaReconciler.CreateTableStatements(db, new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal(TablesOf(db).Count, sql.Count(s => s.Contains("CREATE TABLE", StringComparison.Ordinal)));
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IColumn Colonna(DbContext db, string tabella, string colonna) =>
        db.Model.GetRelationalModel().Tables.Single(t => t.Name == tabella).Columns.Single(c => c.Name == colonna);

    /// <summary>
    /// 🔴 Il verso delle procedure. Su questo percorso la colonna <c>Kind</c> la aggiunge il reconciler, non una
    /// migrazione, e le righe che c'erano già sono tutte SID: se nascessero con un valore che l'enum non sa
    /// rileggere, la prima lettura della tabella esploderebbe — con le ~1470 righe ancora tutte al loro posto.
    /// </summary>
    [Fact]
    public void Il_verso_delle_procedure_si_backfilla_con_Sid()
    {
        using var db = NpgsqlModel();

        Assert.Equal("'Sid'", PostgresSchemaReconciler.BackfillLiteral(Colonna(db, "AirportProcedures", "Kind")));
    }

    /// <summary>
    /// La guardia di CLASSE, e non del caso singolo: gli enum si salvano come stringa (SPEC §6) e si rileggono
    /// in modo non tollerante, quindi il ripiego per tipo store — la stringa vuota — non è il nome di nessun
    /// valore. Una colonna enum NOT NULL aggiunta a una tabella che ha già righe deve nascere con un nome che
    /// l'enum conosce, o il guasto non è un dato sbagliato: è la tabella illeggibile.
    /// </summary>
    [Fact]
    public void Nessuna_colonna_enum_si_backfilla_con_la_stringa_vuota()
    {
        using var db = NpgsqlModel();

        var colpevoli = db.Model.GetRelationalModel().Tables
            .SelectMany(t => t.Columns.Select(c => (Tabella: t.Name, Colonna: c)))
            .Where(x => !x.Colonna.IsNullable)
            .Where(x => x.Colonna.PropertyMappings.Any(m =>
                (Nullable.GetUnderlyingType(m.Property.ClrType) ?? m.Property.ClrType).IsEnum))
            .Where(x => PostgresSchemaReconciler.BackfillLiteral(x.Colonna) == "''")
            .Select(x => $"{x.Tabella}.{x.Colonna.Name}")
            .ToList();

        Assert.True(colpevoli.Count == 0,
            "colonne enum che nascerebbero con una stringa vuota: " + string.Join(", ", colpevoli));
    }

    /// <summary>
    /// Una tabella rinominata si porta dietro indici e vincoli col NOME VECCHIO: senza rinominarli il passo
    /// degli indici ne crea un secondo identico, e la prima migrazione futura che prova a lasciar cadere quel
    /// vincolo per nome fallisce.
    /// </summary>
    [Fact]
    public void Rinominare_una_tabella_rinomina_indici_e_vincoli_rimasti_col_nome_vecchio()
    {
        var sql = PostgresSchemaReconciler.RinomineDiOggetti(
            "AirportSids", "AirportProcedures",
            vincoli: new[] { "FK_AirportSids_Airports_AirportId", "PK_AirportSids" },
            indici: new[] { "IX_AirportSids_AirportId_Order", "PK_AirportSids" });

        Assert.Equal(new[]
        {
            // I vincoli PRIMA: rinominare un vincolo rinomina anche l'indice che lo sostiene.
            "ALTER TABLE \"AirportProcedures\" RENAME CONSTRAINT \"FK_AirportSids_Airports_AirportId\" TO \"FK_AirportProcedures_Airports_AirportId\"",
            "ALTER TABLE \"AirportProcedures\" RENAME CONSTRAINT \"PK_AirportSids\" TO \"PK_AirportProcedures\"",
            "ALTER INDEX \"IX_AirportSids_AirportId_Order\" RENAME TO \"IX_AirportProcedures_AirportId_Order\"",
        }, sql);
    }

    [Fact]
    public void Un_oggetto_che_non_cita_il_nome_vecchio_si_lascia_stare()
    {
        var sql = PostgresSchemaReconciler.RinomineDiOggetti(
            "AirportSids", "AirportProcedures",
            vincoli: Array.Empty<string>(),
            indici: new[] { "IX_AirportProcedures_AirportId_Order", "un_indice_a_mano" });

        Assert.Empty(sql);
    }
}
