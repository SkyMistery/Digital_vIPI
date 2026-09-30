using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Il deposito del registro degli accessi su SQLite (30 settembre 2026).</summary>
public sealed class RegistroAccessiStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private static readonly DateTime Oggi = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);

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

    [Fact]
    public async Task Una_riga_per_VID_e_i_giorni_si_contano()
    {
        var s = new EfRegistroAccessiStore(_db);
        await s.RegistraAsync(704798, "Carmine Granato", "IT", "LIRR", Oggi);
        await s.RegistraAsync(704798, "Carmine Granato", "IT", "LIRR", Oggi.AddHours(2));
        await s.RegistraAsync(704798, "Carmine Granato", "IT", "LIRR", Oggi.AddDays(1));

        var riga = Assert.Single(await _db.AccessiAlSito.AsNoTracking().ToListAsync());
        Assert.Equal(2, riga.Giorni);
        Assert.Equal(Oggi, riga.PrimoUtc);
    }

    [Fact]
    public async Task Si_cerca_per_nome_senza_maiuscole_o_per_inizio_del_VID_i_recenti_per_primi()
    {
        var s = new EfRegistroAccessiStore(_db);
        await s.RegistraAsync(704798, "Carmine Granato", "IT", "LIRR", Oggi);
        await s.RegistraAsync(123456, "Mario Rossi", "IT", "LIMM", Oggi.AddHours(1));
        await s.RegistraAsync(704111, "Jean Dupont", "FR", "LFFF", Oggi.AddHours(2));

        var tutti = await s.ElencoAsync(null, 500);
        Assert.Equal(new[] { 704111, 123456, 704798 }, tutti.Righe.Select(r => r.UserId));
        Assert.Equal(3, tutti.Totale);

        Assert.Equal(new[] { 123456 }, (await s.ElencoAsync("rossi", 500)).Righe.Select(r => r.UserId));
        Assert.Equal(new[] { 704111, 704798 }, (await s.ElencoAsync("704", 500)).Righe.Select(r => r.UserId));

        var limitato = await s.ElencoAsync(null, 2);
        Assert.Equal(2, limitato.Righe.Count);
        Assert.Equal(3, limitato.Totale);
    }

    [Fact]
    public async Task Trova_da_la_riga_del_VID_o_null()
    {
        var s = new EfRegistroAccessiStore(_db);
        await s.RegistraAsync(704798, "Carmine Granato", "IT", "LIRR", Oggi);

        Assert.Equal("Carmine Granato", (await s.TrovaAsync(704798))!.Nome);
        Assert.Null(await s.TrovaAsync(123456));
    }

    [Fact]
    public async Task La_potatura_toglie_chi_non_entra_da_prima_della_soglia()
    {
        var s = new EfRegistroAccessiStore(_db);
        await s.RegistraAsync(1, "Vecchio", "IT", "LIRR", Oggi.AddDays(-400));
        await s.RegistraAsync(2, "Recente", "IT", "LIRR", Oggi.AddDays(-10));

        Assert.Equal(1, await s.PotaAsync(Oggi.AddDays(-365)));
        Assert.Equal(new[] { 2 }, await _db.AccessiAlSito.Select(a => a.UserId).ToListAsync());
    }
}
