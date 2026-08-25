using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il registro sopravvive a un'azione che questa versione non conosce (25 agosto 2026).
///
/// <para>Il registro è <b>append-only e attraversa le versioni</b>: ci finiscono righe scritte da codice più
/// nuovo di quello che le rilegge — un ramo non ancora fuso, o un rollback in produzione. Prima, una riga sola
/// con un'azione ignota faceva esplodere la query e la pagina del Registro moriva INTERA: misurato con due righe
/// <c>View</c> scritte dal ramo <c>statistiche-atc</c>, che uccidevano <c>/services/vsop/admin/audit</c> su
/// <c>main</c>. Ed è proprio il registro che si va a leggere quando qualcosa è andato storto.</para>
/// </summary>
public class AuditAzioneSconosciutaTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>SQL grezzo: EF non scriverebbe mai un valore fuori dall'enum, ed è esattamente il punto —
    /// quella riga la scrive un'ALTRA versione del programma.</summary>
    private async Task ScriviGrezzaAsync(string azione)
    {
        // ⚠️ Parametri e non interpolazione: `ExecuteSqlRaw` passa la stringa per `string.Format`, e le graffe
        // del JSON dei dettagli verrebbero lette come segnaposto («Failure to parse near offset 174»).
        await _db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AuditLogs" ("UserId", "Action", "EntityType", "EntityId", "TimestampUtc", "DetailsJson")
            VALUES (704798, {0}, 'StatsProfile', '562466', '2026-08-25 10:01:52', {1})
            """,
            azione, """{"Vid":562466}""");
    }

    [Fact]
    public async Task Una_azione_sconosciuta_si_legge_come_ignota_e_non_abbatte_la_query()
    {
        await ScriviGrezzaAsync("QualcosaCheNonEsiste");

        var righe = await new EfAuditLogReader(_db).ListRecentAsync();

        var riga = Assert.Single(righe);
        Assert.Equal(AuditAction.Unknown, riga.Action);
        // La riga resta LEGGIBILE: si perde il nome dell'azione, non il resto.
        Assert.Equal("StatsProfile", riga.EntityType);
        Assert.Equal("562466", riga.EntityId);
        Assert.Equal(704798, riga.UserId);
        Assert.Contains("562466", riga.DetailsJson);
    }

    [Fact]
    public async Task Una_riga_sconosciuta_non_nasconde_quelle_buone()
    {
        // È il caso vero: un archivio MISTO. Prima bastava la riga ignota a portarsi via tutte le altre.
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = 1, Action = AuditAction.Publish, EntityType = "DocumentVersion", EntityId = "7",
            TimestampUtc = new DateTime(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc), DetailsJson = "{}",
        });
        await _db.SaveChangesAsync();
        await ScriviGrezzaAsync("QualcosaCheNonEsiste");

        var righe = await new EfAuditLogReader(_db).ListRecentAsync();

        Assert.Equal(2, righe.Count);
        Assert.Contains(righe, r => r.Action == AuditAction.Publish);
        Assert.Contains(righe, r => r.Action == AuditAction.Unknown);
    }

    [Fact]
    public async Task Un_numero_al_posto_del_nome_non_diventa_un_valore_dell_enum()
    {
        // ⚠️ `Enum.TryParse` accetta anche la forma numerica: senza il controllo su IsDefined, un '3' in colonna
        // diventerebbe `Archive` in silenzio — un'azione SBAGLIATA, che è peggio di un'azione ignota.
        await ScriviGrezzaAsync("3");

        var righe = await new EfAuditLogReader(_db).ListRecentAsync();

        Assert.Equal(AuditAction.Unknown, Assert.Single(righe).Action);
    }

    [Fact]
    public async Task Le_azioni_note_restano_quelle_che_sono()
    {
        await ScriviGrezzaAsync("View");
        await ScriviGrezzaAsync("ForceUnlock");

        var azioni = (await new EfAuditLogReader(_db).ListRecentAsync()).Select(r => r.Action).ToList();

        Assert.Contains(AuditAction.View, azioni);
        Assert.Contains(AuditAction.ForceUnlock, azioni);
        Assert.DoesNotContain(AuditAction.Unknown, azioni);
    }
}
