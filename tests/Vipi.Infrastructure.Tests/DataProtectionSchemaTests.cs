using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Dove vivono le chiavi Data Protection, per provider.
///
/// <para>Perché è coperto da test e non solo dalla verifica live: la risposta sbagliata è <b>silenziosa</b>.
/// Un provider che non tiene il key-ring nel database ricade sul file-store, l'applicazione parte, le pagine
/// si aprono, e il guasto compare solo dopo un riavvio — sotto forma di utenti sloggati e form che rifiutano
/// l'antiforgery token. È esattamente com'è sopravvissuto il ramo MariaDB fino ad oggi.</para>
/// </summary>
public class DataProtectionSchemaTests
{
    [Theory]
    [InlineData(PersistenceProvider.Postgres)]
    [InlineData(PersistenceProvider.MySql)]
    public void Senza_cartella_configurata_i_due_deploy_tengono_le_chiavi_nel_database(PersistenceProvider provider)
        => Assert.Equal(DataProtectionSchema.KeyRingStore.Database,
            DataProtectionSchema.ResolveStore(provider, null));

    [Fact]
    public void In_sviluppo_su_Sqlite_resta_il_file_store()
        => Assert.Equal(DataProtectionSchema.KeyRingStore.DefaultFileSystem,
            DataProtectionSchema.ResolveStore(PersistenceProvider.Sqlite, null));

    /// <summary>
    /// La regola che tiene le chiavi fuori dal database del committente. Su <c>atc.it.ivao.aero</c> il
    /// key-ring è XML in chiaro in una tabella di un database che non è nostro: chi ha <c>SELECT</c> lì può
    /// firmare un cookie di sessione per qualunque VID. La cartella configurata deve vincere <b>su ogni
    /// provider</b>, MySQL compreso — che è l'unico caso che conta davvero.
    /// </summary>
    [Theory]
    [InlineData(PersistenceProvider.MySql)]
    [InlineData(PersistenceProvider.Postgres)]
    [InlineData(PersistenceProvider.Sqlite)]
    public void Una_cartella_configurata_vince_su_qualunque_provider(PersistenceProvider provider)
        => Assert.Equal(DataProtectionSchema.KeyRingStore.ConfiguredFolder,
            DataProtectionSchema.ResolveStore(provider, "/var/lib/vipi/keys"));

    /// <summary>
    /// Una cartella vuota o di soli spazi non è una cartella: deve valere come «non configurata», o un
    /// <c>DataProtection__KeyRingPath=</c> lasciato vuoto nell'ambiente manderebbe le chiavi in una
    /// directory dal nome vuoto invece che nel posto previsto.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Una_cartella_vuota_vale_come_non_configurata(string? percorso)
        => Assert.Equal(DataProtectionSchema.KeyRingStore.Database,
            DataProtectionSchema.ResolveStore(PersistenceProvider.MySql, percorso));

    /// <summary>
    /// Il file di deploy vero deve configurare la cartella: è lì che la decisione ha effetto. Un test sulla
    /// funzione pura non direbbe niente se poi in produzione la chiave di configurazione mancasse.
    /// </summary>
    [Fact]
    public void Il_deploy_di_produzione_tiene_le_chiavi_fuori_dal_database()
    {
        var percorso = Path.Combine(RadiceRepo(), "deploy", "atc-ivao", "appsettings.Production.json");
        Assert.True(File.Exists(percorso), $"atteso il file di deploy in {percorso}");

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(percorso));
        Assert.True(doc.RootElement.TryGetProperty("DataProtection", out var dp),
            "appsettings.Production.json non configura DataProtection: le chiavi finirebbero nel database " +
            "del committente, dove sono leggibili in chiaro.");

        var keyRing = dp.GetProperty("KeyRingPath").GetString();
        Assert.False(string.IsNullOrWhiteSpace(keyRing));

        // Percorso assoluto: uno relativo si risolverebbe rispetto alla directory di lavoro del processo,
        // che su un host a pannello non è detto sia quella dell'applicazione.
        Assert.StartsWith("/", keyRing, StringComparison.Ordinal);

        // NON dentro la cartella dell'applicazione, che si sovrascrive a ogni aggiornamento: lì le chiavi
        // sopravvivrebbero fino al primo deploy (tutti sloggati, ogni volta) e sarebbero per giunta
        // scaricabili via HTTP dove il documento radice coincide con la cartella dell'applicazione.
        //
        // ⚠️ Il vincolo era «sotto /var/lib/», che era la cartella di stato creata da systemd. Non regge
        // più dal 15 agosto 2026: atc.it.ivao.aero gira su Plesk+Passenger come utente della
        // sottoscrizione, che sotto /var/lib non può creare nulla — e l'applicazione moriva all'avvio con
        // «Access to the path '/var/lib/vipi' is denied». L'invariante vera non è mai stata QUALE cartella,
        // ma che stia fuori da quella di deploy.
        foreach (var cartellaDiDeploy in new[] { "/opt/vipi", "public_atc" })
            Assert.DoesNotContain(cartellaDiDeploy, keyRing, StringComparison.Ordinal);
    }

    private static string RadiceRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vipi.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>
    /// Ogni provider che dichiara di tenere le chiavi nel DB deve avere una DDL: è la coppia che si
    /// disallinea per prima quando se ne aggiunge uno.
    /// </summary>
    [Fact]
    public void Ogni_provider_con_key_ring_su_database_ha_la_propria_ddl()
    {
        foreach (var provider in Enum.GetValues<PersistenceProvider>())
        {
            if (!DataProtectionSchema.UsesDatabaseKeyRing(provider)) continue;

            var ddl = DataProtectionSchema.CreateTableSql(provider);
            Assert.Contains("CREATE TABLE IF NOT EXISTS", ddl);       // idempotente: gira a ogni avvio
            Assert.Contains(DataProtectionSchema.TableName, ddl, StringComparison.Ordinal);
            // Le tre colonne attese da EntityFrameworkCoreXmlRepository.
            Assert.Contains("Id", ddl, StringComparison.Ordinal);
            Assert.Contains("FriendlyName", ddl, StringComparison.Ordinal);
            Assert.Contains("Xml", ddl, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Su Linux <c>lower_case_table_names</c> è 0: <c>dataprotectionkeys</c> e <c>DataProtectionKeys</c> sono
    /// due tabelle diverse, ed EF cerca quella con le maiuscole (convenzione sul nome del DbSet). Creandola
    /// minuscola l'avvio non fallirebbe — fallirebbe il primo accesso al key-ring, in produzione.
    /// </summary>
    [Fact]
    public void Il_nome_della_tabella_e_quello_che_cerca_ef_maiuscole_comprese()
    {
        Assert.Equal("DataProtectionKeys", DataProtectionSchema.TableName);
        Assert.Contains($"`{DataProtectionSchema.TableName}`", DataProtectionSchema.CreateTableSql(PersistenceProvider.MySql));
        Assert.Contains($"\"{DataProtectionSchema.TableName}\"", DataProtectionSchema.CreateTableSql(PersistenceProvider.Postgres));
    }

    /// <summary>
    /// La tabella delle chiavi non è nel modello EF, quindi <c>MySqlCollation.Apply</c> non la tocca: se non
    /// dichiarasse la collation nella propria DDL erediterebbe il default del database — l'unica tabella
    /// dello schema a comportarsi diversamente dalle altre.
    /// </summary>
    [Fact]
    public void Su_mysql_la_ddl_dichiara_la_collation_del_resto_dello_schema()
    {
        var ddl = DataProtectionSchema.CreateTableSql(PersistenceProvider.MySql);
        Assert.Contains(MySqlCollation.Name, ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Chiedere_la_ddl_a_un_provider_senza_key_ring_su_database_fallisce_dicendo_perche()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DataProtectionSchema.CreateTableSql(PersistenceProvider.Sqlite));

        Assert.Contains(nameof(DataProtectionSchema.UsesDatabaseKeyRing), ex.Message, StringComparison.Ordinal);
    }
}
