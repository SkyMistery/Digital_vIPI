using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Chi ha usato quale account dell'evento finisce nel registro di audit (carta 2026-10-01-account-evento.md).</summary>
public sealed class AccountEventoTracciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>
    /// 🔴 Il riavvio (committente, 1 ottobre 2026): la copia nel database si scrive, si aggiorna, si toglie, e all'avvio
    /// <see cref="AccountEventoAvvio"/> la rimette in un registro nuovo.
    /// </summary>
    [Fact]
    public async Task L_archivio_si_scrive_si_toglie_e_all_avvio_rimette_in_piedi_il_registro()
    {
        var log = Microsoft.Extensions.Logging.Abstractions.NullLogger<EfAccountEventoArchivio>.Instance;
        var archivio = new EfAccountEventoArchivio(_db, log);
        var fine = DateTime.UtcNow.AddHours(3);
        await archivio.SalvaAsync(704798, 600100, fine);
        await archivio.SalvaAsync(704798, 600101, fine);    // stessa persona: si aggiorna, non si raddoppia
        await archivio.SalvaAsync(123456, 600200, fine);
        await archivio.TogliAsync(123456);

        var servizi = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<Vipi.Application.EventKits.IAccountEventoArchivio>(archivio)
            .BuildServiceProvider();
        var registro = new Vipi.Application.EventKits.AccountEventoRegistro();
        await new AccountEventoAvvio(servizi.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(), registro)
            .StartAsync(default);

        Assert.Equal(600101, registro.VidPer(704798, DateTime.UtcNow));
        Assert.Null(registro.VidPer(123456, DateTime.UtcNow));

        await archivio.SvuotaAsync();
        Assert.Empty(await archivio.TuttiAsync());
    }

    /// <summary>La lista dei VID con la sua data si salva, e la pulizia la cancella lasciando il resto dell'evento.</summary>
    [Fact]
    public async Task La_lista_si_salva_con_la_data_e_la_pulizia_la_cancella()
    {
        var repo = new EfEventKitRepository(_db);
        var il = new DateTime(2026, 10, 9, 6, 0, 0, DateTimeKind.Utc);
        await repo.SaveHeaderAsync("Italian Night Ops", true, null, null, 704798, "Prova", DateTime.UtcNow);
        await repo.SaveVidAsync("600100 LIRF_TWR", il, 704798, "Prova", DateTime.UtcNow);

        var kit = (await repo.LoadAsync())!.Testata;
        Assert.Equal("600100 LIRF_TWR", kit.VidEvento);
        Assert.Equal(il, kit.VidSvuotaUtc);

        Assert.True(await repo.ClearVidAsync());
        Assert.False(await repo.ClearVidAsync());
        kit = (await repo.LoadAsync())!.Testata;
        Assert.Null(kit.VidEvento);
        Assert.Null(kit.VidSvuotaUtc);
        Assert.Equal("Italian Night Ops", kit.Name);
    }

    /// <summary>Una riga per persona e VID al giorno: chi riscrive il VID dopo un ricarico non ne lascia dieci.</summary>
    [Fact]
    public async Task Una_riga_per_persona_e_VID_al_giorno_con_la_postazione()
    {
        var t = new EfAccountEventoTraccia(_db);
        await t.RegistraAsync(704798, 600100, "LIRF_TWR");
        await t.RegistraAsync(704798, 600100, "LIRF_TWR");
        await t.RegistraAsync(704798, 600101, "LIRF_APP");

        var righe = await _db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "EventAccount").OrderBy(a => a.EntityId).ToListAsync();
        Assert.Equal(new[] { "600100", "600101" }, righe.Select(r => r.EntityId));
        Assert.All(righe, r => Assert.Equal(AuditAction.View, r.Action));
        Assert.Contains("LIRF_TWR", righe[0].DetailsJson);
    }
}
