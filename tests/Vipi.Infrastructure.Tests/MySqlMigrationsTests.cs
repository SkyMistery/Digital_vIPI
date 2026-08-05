using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Vipi.Infrastructure.MySqlMigrations;
using Vipi.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il set di migrazioni MySQL vive in un assembly a sé (le 68 del repo sono SQLite-flavored e non girano
/// su MySQL). La scelta ha un costo dichiarato in ADR-0007 §D4-bis: <b>ogni cambio di schema va emesso due
/// volte</b>. Questi test rendono quel costo esigibile in CI invece che al deploy.
///
/// <para>Nessuno richiede un MySQL vivo: la generazione dello script legge il modello e non apre connessioni.</para>
/// </summary>
public class MySqlMigrationsTests
{
    private readonly ITestOutputHelper _out;
    public MySqlMigrationsTests(ITestOutputHelper output) => _out = output;

    /// <summary>Stesso wiring che userà l'host: provider MySQL + assembly di migrazioni dedicato.</summary>
    private static VipiDbContext Contesto() =>
        new MySqlMigrationsDesignTimeFactory().CreateDbContext([]);

    /// <summary>
    /// L'assembly delle migrazioni si indica per <b>nome</b> (<see cref="MySqlSchema.MigrationsAssemblyName"/>),
    /// perché il riferimento inverso sarebbe un ciclo di progetti. Il prezzo è che una rinomina non rompe la
    /// compilazione: romperebbe l'avvio in produzione, con un messaggio EF che parla di migrazioni mancanti
    /// invece che di un assembly introvabile. Questo test rimette il fallimento dove si vede.
    /// </summary>
    [Fact]
    public void Il_nome_dellassembly_delle_migrazioni_e_quello_vero()
    {
        Assert.Equal(
            MySqlSchema.MigrationsAssemblyName,
            typeof(MySqlMigrationsDesignTimeFactory).Assembly.GetName().Name);
    }

    /// <summary>
    /// La guardia che conta. Se qualcuno aggiunge un'entità, una colonna o un indice e rigenera solo le
    /// migrazioni SQLite, qui il modello e lo snapshot MySQL divergono e il test lo dice — con l'elenco
    /// delle differenze. Senza, la divergenza si scoprirebbe su <c>atc.it.ivao.aero</c>, come colonna
    /// mancante a runtime.
    ///
    /// <para>Rimedio quando fallisce: <c>dotnet ef migrations add &lt;Nome&gt; --project
    /// src/Vipi.Infrastructure.MySqlMigrations --startup-project src/Vipi.Infrastructure.MySqlMigrations
    /// -o Migrations</c>.</para>
    /// </summary>
    [Fact]
    public void Le_migrazioni_mysql_sono_allineate_al_modello()
    {
        using var db = Contesto();

        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        // Lo snapshot è un modello "grezzo": va finalizzato come farebbe EF, o il confronto vede
        // differenze che non esistono.
        var modelloSnapshot = db.GetService<IModelRuntimeInitializer>()
            .Initialize((IModel)snapshot!.Model, designTime: true, validationLogger: null);

        var differenze = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            modelloSnapshot.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        foreach (var op in differenze) _out.WriteLine(op.GetType().Name);

        Assert.True(differenze.Count == 0,
            $"il modello ha {differenze.Count} differenze non ancora emesse come migrazione MySQL " +
            "(vedi l'output del test per i tipi di operazione mancanti)");
    }

    /// <summary>
    /// La collation non si legge dal modello, si legge dalla DDL: il provider la dichiara nelle operazioni
    /// di migrazione ma non la emette in SQL. L'unica cosa che la porta davvero nel database è lo statement
    /// <c>ALTER DATABASE</c>, aggiunto a mano nella migrazione iniziale — quindi il test verifica proprio
    /// quello, e non che il modello «abbia la collation», che è vero anche quando il database non ce l'ha.
    /// </summary>
    [Fact]
    public void Lo_script_imposta_la_collation_prima_di_creare_le_tabelle()
    {
        using var db = Contesto();
        var sql = db.GetService<IMigrator>().GenerateScript();

        var posizioneAlter = sql.IndexOf(MySqlCollation.AlterDatabaseSql, StringComparison.OrdinalIgnoreCase);
        Assert.True(posizioneAlter >= 0,
            $"lo script non contiene «{MySqlCollation.AlterDatabaseSql}»: senza, ogni tabella nasce con la " +
            "collation di default del server, che è case- e accent-INsensitive");

        // Deve precedere la prima tabella del modello: in MySQL l'ereditarietà del charset si decide al
        // CREATE TABLE, quindi arrivare dopo non avrebbe alcun effetto su quelle già create.
        var posizionePrimaTabella = sql.IndexOf("CREATE TABLE `Accs`", StringComparison.OrdinalIgnoreCase);
        Assert.True(posizionePrimaTabella > 0, "lo script non crea le tabelle del modello");
        Assert.True(posizioneAlter < posizionePrimaTabella,
            "l'ALTER DATABASE arriva dopo la prima CREATE TABLE: le tabelle create prima non ereditano la collation");
    }

    /// <summary>
    /// Le lunghezze della slice S1 devono arrivare fino alla DDL, non fermarsi al modello: è il loro unico
    /// scopo. Una colonna indicizzata rimasta <c>longtext</c> fa fallire il <c>CREATE TABLE</c> su InnoDB.
    /// </summary>
    [Fact]
    public void Nessuna_colonna_indicizzata_resta_longtext_nella_ddl()
    {
        using var db = Contesto();
        var sql = db.GetService<IMigrator>().GenerateScript();

        var longtextIndicizzate = new List<string>();
        foreach (var (entita, proprieta) in MySqlStringLengths.Map.Keys)
        {
            var tabella = db.Model.GetEntityTypes().First(e => e.ClrType.Name == entita).GetTableName()!;
            var riga = TrovaDefinizioneColonna(sql, tabella, proprieta);

            Assert.True(riga is not null, $"colonna {tabella}.{proprieta} assente dalla DDL generata");
            if (riga!.Contains("longtext", StringComparison.OrdinalIgnoreCase))
                longtextIndicizzate.Add($"{tabella}.{proprieta}");
        }

        Assert.True(longtextIndicizzate.Count == 0,
            "colonne indicizzate emesse come longtext (InnoDB non le indicizza): " + string.Join(", ", longtextIndicizzate));
    }

    /// <summary>
    /// Cerca la definizione di una colonna <b>dentro il blocco della sua tabella</b>. Cercarla nell'intero
    /// script per solo nome dà falsi positivi: <c>Status</c>, <c>Type</c> e <c>AiracCycle</c> esistono in
    /// più tabelle, indicizzate in una e libere in un'altra, e la prima riga trovata è quella sbagliata.
    /// </summary>
    private static string? TrovaDefinizioneColonna(string sql, string tabella, string colonna)
    {
        var inizio = sql.IndexOf($"CREATE TABLE `{tabella}` (", StringComparison.Ordinal);
        if (inizio < 0) return null;

        var fine = sql.IndexOf("\n);", inizio, StringComparison.Ordinal);
        if (fine < 0) fine = sql.Length;

        return sql[inizio..fine].Split('\n')
            .FirstOrDefault(r => r.TrimStart().StartsWith($"`{colonna}` ", StringComparison.Ordinal));
    }
}
