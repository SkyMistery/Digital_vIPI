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
    public void I_due_deploy_tengono_le_chiavi_nel_database(PersistenceProvider provider)
        => Assert.True(DataProtectionSchema.UsesDatabaseKeyRing(provider));

    [Fact]
    public void In_sviluppo_su_Sqlite_resta_il_file_store()
        => Assert.False(DataProtectionSchema.UsesDatabaseKeyRing(PersistenceProvider.Sqlite));

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
