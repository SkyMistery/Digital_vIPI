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
}
