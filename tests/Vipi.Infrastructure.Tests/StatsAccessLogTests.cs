using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'unica LETTURA che finisce nel registro: lo staff ha aperto le statistiche personali di un altro.
///
/// <para>Due cose vanno protette insieme, e tirano in direzioni opposte. Che la riga <b>ci sia</b>: senza,
/// l'accesso ai dati di qualcun altro non è controllato e la fascia in pagina promette una cosa che non
/// succede. E che le righe <b>non siano venti</b>: la pagina si ricarica a ogni chip di periodo e a ogni
/// F5, e un registro con venti righe identiche a mezzo minuto l'una dall'altra non si legge — che è come
/// non averlo.</para>
/// </summary>
public class StatsAccessLogTests : IAsyncLifetime
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

    private Task<List<Domain.Entities.AuditLog>> Righe() =>
        _db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "StatsProfile").ToListAsync();

    [Fact]
    public async Task Aprire_le_statistiche_di_un_altro_lascia_una_riga_col_VID_guardato()
    {
        await new EfStatsAccessLog(_db).RecordProfileViewAsync(actorUserId: 704798, subjectUserId: 555003);

        var riga = Assert.Single(await Righe());
        Assert.Equal(704798, riga.UserId);                 // chi ha guardato
        Assert.Equal("555003", riga.EntityId);             // chi è stato guardato
        Assert.Equal(AuditAction.View, riga.Action);
        Assert.Contains("555003", riga.DetailsJson);
    }

    /// <summary>Le proprie non sono un accesso ai dati di un altro: nessuna riga, mai.</summary>
    [Fact]
    public async Task Le_proprie_statistiche_non_si_registrano()
    {
        await new EfStatsAccessLog(_db).RecordProfileViewAsync(704798, 704798);
        Assert.Empty(await Righe());
    }

    /// <summary>
    /// ⚠️ Cinque chip di periodo premuti di fila sono UNA consultazione, non cinque accessi.
    /// </summary>
    [Fact]
    public async Task Gli_accessi_ravvicinati_alla_stessa_persona_fanno_una_riga_sola()
    {
        var log = new EfStatsAccessLog(_db);
        for (var i = 0; i < 5; i++)
            await log.RecordProfileViewAsync(704798, 555003);

        Assert.Single(await Righe());
    }

    /// <summary>L'accorpamento è per COPPIA: guardare due persone sono due accessi, non uno.</summary>
    [Fact]
    public async Task Persone_diverse_fanno_righe_diverse()
    {
        var log = new EfStatsAccessLog(_db);
        await log.RecordProfileViewAsync(704798, 555003);
        await log.RecordProfileViewAsync(704798, 555004);

        Assert.Equal(new[] { "555003", "555004" }, (await Righe()).Select(r => r.EntityId).Order());
    }

    /// <summary>E per ATTORE: due staffisti che guardano la stessa persona sono due accessi da spiegare.</summary>
    [Fact]
    public async Task Staffisti_diversi_fanno_righe_diverse()
    {
        var log = new EfStatsAccessLog(_db);
        await log.RecordProfileViewAsync(704798, 555003);
        await log.RecordProfileViewAsync(704799, 555003);

        Assert.Equal(2, (await Righe()).Count);
    }

    /// <summary>
    /// Passata la finestra, la consultazione è nuova. Il tempo non si può far scorrere, ma si può invecchiare
    /// la riga già scritta: è la stessa condizione che vede la query.
    /// </summary>
    [Fact]
    public async Task Dopo_la_finestra_una_nuova_visita_lascia_una_riga_nuova()
    {
        var log = new EfStatsAccessLog(_db);
        await log.RecordProfileViewAsync(704798, 555003);

        var vecchia = await _db.AuditLogs.SingleAsync(a => a.EntityType == "StatsProfile");
        vecchia.TimestampUtc = DateTime.UtcNow.AddHours(-2);
        await _db.SaveChangesAsync();

        await log.RecordProfileViewAsync(704798, 555003);
        Assert.Equal(2, (await Righe()).Count);
    }
}
