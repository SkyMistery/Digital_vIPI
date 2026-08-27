using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// C6 — la chiave di release di una vIPI ACC è <c>{acc}|{callsign del primario}</c>: la spostano un primario
/// che cambia o un settore riparentato, e le release restano scritte sotto la vecchia. Il gate pubblico
/// cerca la nuova, non trova niente, e <b>il documento pubblicato va muto</b>. Qui si prova il motore che
/// rimette il puntatore a posto — e quello che si RIFIUTA di fare.
/// </summary>
public class RipuntamentoChiaveReleaseTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfReleaseRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = TestReleaseTargets.ReleaseRepo(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task Release(string key, int versione)
    {
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.AccVipi, TargetKey = key, VersionNumber = versione,
            ReleaseAiracCycle = "2608", ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-1),
            Status = ReleaseStatus.Effective, PayloadJson = "{}", CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Le_release_e_gli_incarichi_seguono_la_chiave_viva()
    {
        await Release("LIBB|LIBB_VECCHIO_CTR", 1);
        await Release("LIBB|LIBB_VECCHIO_CTR", 2);
        _db.EditorTasks.Add(new EditorTask
        {
            TargetType = ReleaseTargetType.AccVipi, TargetKey = "LIBB|LIBB_VECCHIO_CTR",
            Title = "Rivedere l'AoR", CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var mosse = await _repo.RepointKeyAsync(ReleaseTargetType.AccVipi,
            "LIBB|LIBB_VECCHIO_CTR", "LIBB|LIBB_ES_CTR");

        Assert.Equal(2, mosse);
        Assert.Equal(2, await _db.DocReleases.CountAsync(r => r.TargetKey == "LIBB|LIBB_ES_CTR"));
        Assert.Equal(0, await _db.DocReleases.CountAsync(r => r.TargetKey == "LIBB|LIBB_VECCHIO_CTR"));
        Assert.Equal("LIBB|LIBB_ES_CTR", (await _db.EditorTasks.SingleAsync()).TargetKey);

        // La riscrittura di un puntatore su documenti pubblicati non passa muta, anche se non l'ha fatta una persona.
        var riga = await _db.AuditLogs.SingleAsync();
        Assert.Equal(AuditAction.Update, riga.Action);
        Assert.Equal("DocRelease", riga.EntityType);
        Assert.Equal(0, riga.UserId);                       // il giro notturno
        Assert.Contains("LIBB|LIBB_VECCHIO_CTR", riga.DetailsJson);
    }

    [Fact]
    public async Task Se_la_chiave_nuova_ha_gia_una_sua_storia_non_si_fonde_niente()
    {
        await Release("LIBB|LIBB_VECCHIO_CTR", 1);
        await Release("LIBB|LIBB_ES_CTR", 1);               // stessa versione: fondere darebbe due #1

        var mosse = await _repo.RepointKeyAsync(ReleaseTargetType.AccVipi,
            "LIBB|LIBB_VECCHIO_CTR", "LIBB|LIBB_ES_CTR");

        Assert.Equal(0, mosse);
        Assert.Equal(1, await _db.DocReleases.CountAsync(r => r.TargetKey == "LIBB|LIBB_VECCHIO_CTR"));
        Assert.Empty(await _db.AuditLogs.ToListAsync());    // non è successo niente da raccontare
    }

    [Fact]
    public async Task Una_chiave_di_un_altro_TIPO_non_si_tocca()
    {
        await Release("LIBB|LIBB_VECCHIO_CTR", 1);

        var mosse = await _repo.RepointKeyAsync(ReleaseTargetType.App,
            "LIBB|LIBB_VECCHIO_CTR", "LIBB|LIBB_ES_CTR");

        Assert.Equal(0, mosse);
        Assert.Equal(1, await _db.DocReleases.CountAsync(r => r.TargetKey == "LIBB|LIBB_VECCHIO_CTR"));
    }

    [Fact]
    public async Task Chiave_uguale_o_vuota_e_un_non_evento()
    {
        await Release("LIBB|LIBB_ES_CTR", 1);

        Assert.Equal(0, await _repo.RepointKeyAsync(ReleaseTargetType.AccVipi, "LIBB|LIBB_ES_CTR", "libb|libb_es_ctr"));
        Assert.Equal(0, await _repo.RepointKeyAsync(ReleaseTargetType.AccVipi, "", "LIBB|LIBB_ES_CTR"));
        Assert.Empty(await _db.AuditLogs.ToListAsync());
    }
}
