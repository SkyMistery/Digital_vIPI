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
/// <para>Chi decide è <see cref="DataProtectionSchema.ResolveStore"/>: qui c'è solo il wiring. Tre esiti —
/// una <b>cartella configurata</b> (<c>DataProtection:KeyRingPath</c>) vince su tutto ed è ciò che usa
/// <c>atc.it.ivao.aero</c>; senza cartella, i due deploy tengono le chiavi <b>nel database</b> e lo sviluppo
/// su SQLite resta sul file-store predefinito.</para>
///
/// <para>⚠️ <b>Perché su atc.it.ivao.aero le chiavi NON stanno nel database.</b> Quel database è del
/// committente. Il key-ring è XML in chiaro — su Linux non c'è DPAPI e nulla lo cifra — quindi chi ha
/// <c>SELECT</c> su <c>itivao_atc</c> può fabbricare un cookie di sessione valido per qualunque VID, admin
/// compresi: l'intero modello di autorizzazione poggia sui claim del login IVAO, e questo lo scavalca. Il
/// dump di consegna esclude già la tabella per questa ragione; la tabella viva sul loro server è lo stesso
/// segreto. Su una macchina con systemd la cartella persistente c'è, quindi il motivo per cui il key-ring
/// era finito nel DB — il disco effimero di Render — lì non si applica.</para>
///
/// <para>Il context è separato da <see cref="VipiDbContext"/> per non toccarne modello e migrazioni: la sua
/// unica tabella non compare né fra le entità né nei due set di migrazioni, e si crea a mano all'avvio.</para>
/// </summary>
public static class VipiDataProtection
{
    /// <summary>
    /// Registra il key-store dove <see cref="DataProtectionSchema.ResolveStore"/> dice che debba stare.
    /// Ritorna <c>true</c> se le chiavi finiscono <b>nel database</b>, cioè se
    /// <see cref="UseVipiDataProtection"/> avrà una tabella da creare.
    /// </summary>
    public static bool AddVipiDataProtection(this WebApplicationBuilder builder)
    {
        var provider = PersistenceProviderResolver.Resolve(
            builder.Configuration[PersistenceProviderResolver.ProviderConfigKey]);
        var cartella = builder.Configuration[DataProtectionSchema.KeyRingPathConfigKey];
        var store = DataProtectionSchema.ResolveStore(provider, cartella);

        // Cartella configurata: le chiavi non toccano il database. È il caso di atc.it.ivao.aero, dove il
        // database è del committente e un key-ring leggibile di lì basta a fabbricare una sessione valida
        // per qualunque VID.
        if (store == DataProtectionSchema.KeyRingStore.ConfiguredFolder)
        {
            // Creata qui e non lasciata a Data Protection: se il percorso non è scrivibile — permessi del
            // servizio, StateDirectory mancante — si scopre all'avvio invece che al primo login, dove il
            // sintomo sarebbe «Correlation failed» e non parlerebbe di cartelle.
            //
            // L'eccezione viene riscritta perché quella originale dice solo «Access to the path X is denied»:
            // vera ma muta su ciò che serve fare. Successo il 15 agosto 2026 su atc.it.ivao.aero, dove il
            // percorso era ancora /var/lib/vipi/keys — scritto per il deploy systemd — mentre l'host reale è
            // Plesk + Phusion Passenger e il processo gira come utente della sottoscrizione, che sotto
            // /var/lib non può creare nulla. Il messaggio nomina utente e chiave di configurazione perché
            // chi legge (spesso via FTP, dal file avvio-errore.txt) possa agire senza rimbalzare a noi.
            DirectoryInfo dir;
            try
            {
                dir = Directory.CreateDirectory(cartella!);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                throw new InvalidOperationException(
                    $"Non riesco a creare o usare la cartella delle chiavi Data Protection: «{cartella}». " +
                    $"Il processo gira come utente '{Environment.UserName}', che su quel percorso non ha i " +
                    $"permessi di scrittura. Indicare in appsettings.Production.json una cartella che " +
                    $"QUELL'utente possa scrivere, sotto la chiave '{DataProtectionSchema.KeyRingPathConfigKey}' " +
                    $"— fuori dalla cartella di deploy (altrimenti sparisce a ogni aggiornamento) e fuori dal " +
                    $"documento radice del sito (il key-ring è XML in chiaro: chi lo scarica fabbrica una " +
                    $"sessione valida per qualunque VID).", ex);
            }

            builder.Services.AddDataProtection()
                .SetApplicationName("vIPI")
                .PersistKeysToFileSystem(dir);
            return false;
        }

        // Nessuna cartella: sullo sviluppo (SQLite) resta il file-store predefinito, e non c'è nulla da fare.
        if (store == DataProtectionSchema.KeyRingStore.DefaultFileSystem) return false;

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
