using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Host;

/// <summary>
/// Persistenza delle chiavi Data Protection (antiforgery, cookie di autenticazione, state OIDC). STACCABILE
/// come il modulo auth. Il key-ring predefinito è una cartella sul disco: dove il disco è effimero — il
/// container di Render, o il nostro processo su <c>atc.it.ivao.aero</c> se ripartisse da una macchina
/// riprovisionata — si perde a ogni riavvio, e il sintomo non parla di chiavi ma di utenti sloggati e di form
/// che rispondono «antiforgery token non valido».
///
/// <para>Attivo su <b>Postgres</b> e <b>MySQL/MariaDB</b>, cioè i due deploy; in sviluppo (SQLite) resta il
/// file-store predefinito, adeguato a una macchina sola e senza tabelle in più nel DB di lavoro. Chi decide è
/// <see cref="DataProtectionSchema.UsesDatabaseKeyRing"/>: qui c'è solo il wiring.</para>
///
/// <para>Il context è separato da <see cref="VipiDbContext"/> per non toccarne modello e migrazioni: la sua
/// unica tabella non compare né fra le entità né nei due set di migrazioni, e si crea a mano all'avvio.</para>
/// </summary>
public static class VipiDataProtection
{
    /// <summary>Se il provider tiene le chiavi nel DB, registra il key-store. Ritorna <c>true</c> se attivato.</summary>
    public static bool AddVipiDataProtection(this WebApplicationBuilder builder)
    {
        var provider = PersistenceProviderResolver.Resolve(
            builder.Configuration[PersistenceProviderResolver.ProviderConfigKey]);
        if (!DataProtectionSchema.UsesDatabaseKeyRing(provider)) return false;

        var connectionString = builder.Configuration.GetConnectionString("Vipi");
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        builder.Services.AddDbContext<DataProtectionKeysDbContext>(o =>
        {
            if (provider == PersistenceProvider.Postgres)
            {
                // Stessa resilienza di VipiDbContext (vedi DependencyInjection): Neon sospende il compute e
                // chiude le connessioni idle, quindi la prima query dopo l'inattività fallisce "transient".
                // Senza retry qui, un transient sul key-ring si propaga a tutto ciò che passa da Data
                // Protection — antiforgery, cookie di auth e lo state OIDC — e il login muore con
                // «Correlation failed» invece di riprovare. Copre anche UseVipiDataProtection: ExecuteSqlRaw
                // passa dall'execution strategy, quindi il CREATE TABLE all'avvio non fa fallire il boot se
                // Neon si sta risvegliando.
                o.UseNpgsql(connectionString, npg => npg
                    .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null));
            }
            else
            {
                // MariaDB su atc.it.ivao.aero: server dedicato, non serverless. Niente retry, perché la
                // ragione del retry su Neon — il risveglio del compute sospeso — qui non esiste.
                //
                // Versione FISSATA come in DependencyInjection: ServerVersion.AutoDetect aprirebbe una
                // connessione mentre si costruiscono le opzioni, e col database giù l'applicazione non
                // partirebbe per un motivo che non somiglia a quello vero.
                o.UseMySql(connectionString, MySqlSchema.ResolveServerVersion(
                    builder.Configuration[MySqlSchema.ServerVersionConfigKey]));
            }
        });

        // SetApplicationName fissa il discriminatore di scopo: instanze/redeploy diversi condividono lo stesso
        // key-ring (senza, ogni deploy userebbe uno scopo diverso e i vecchi token resterebbero comunque invalidi).
        builder.Services.AddDataProtection()
            .SetApplicationName("vIPI")
            .PersistKeysToDbContext<DataProtectionKeysDbContext>();

        return true;
    }

    /// <summary>Crea la tabella delle chiavi se manca (idempotente). No-op se il modulo non è attivo.</summary>
    public static WebApplication UseVipiDataProtection(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetService<DataProtectionKeysDbContext>();
        if (ctx is null) return app;

        // Il provider si rilegge dalla configurazione, e la DDL è indicizzata per PersistenceProvider: è la
        // stessa decisione presa in AddVipiDataProtection, che è anche l'unica ragione per cui questo context
        // esiste — se non fosse attivo, la GetService qui sopra avrebbe già ritornato null.
        var provider = PersistenceProviderResolver.Resolve(
            app.Configuration[PersistenceProviderResolver.ProviderConfigKey]);

        ctx.Database.ExecuteSqlRaw(DataProtectionSchema.CreateTableSql(provider));
        return app;
    }
}

/// <summary>Context dedicato al solo key-ring Data Protection (tabella <c>DataProtectionKeys</c>).</summary>
public sealed class DataProtectionKeysDbContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options) : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
