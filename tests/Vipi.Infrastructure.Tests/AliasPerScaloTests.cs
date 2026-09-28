using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-031 (revisione totale 3): l'alias dei fix era globale. La radice risolta in uno scalo riscriveva, senza il
/// segno «da verificare», il punto delle procedure di un altro: LUMA è LUMAR a LIBD e LUMAV a LIPE, a 400 km. E lo
/// creava qualunque Editor, anche di un altro ACC. Ora un alias vale per lo scalo da cui nasce; quelli vecchi, senza
/// scalo, restano validi per tutti.
/// </summary>
public class AliasPerScaloTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSidFixAliasRepository _alias = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _alias = new EfSidFixAliasRepository(_db, LivelloFisso.Editor);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task L_alias_di_uno_scalo_non_vale_per_un_altro()
    {
        await _alias.UpsertAsync("LIBD", "LUMA", "LUMAR");

        Assert.Equal("LUMAR", (await _alias.GetMapAsync("LIBD"))["LUMA"]);
        Assert.False((await _alias.GetMapAsync("LIPE")).ContainsKey("LUMA"));
    }

    [Fact]
    public async Task Lo_stesso_prefisso_vale_un_punto_per_scalo()
    {
        await _alias.UpsertAsync("LIBD", "LUMA", "LUMAR");
        await _alias.UpsertAsync("lipe", "luma", "lumav");

        Assert.Equal("LUMAR", (await _alias.GetMapAsync("LIBD"))["LUMA"]);
        Assert.Equal("LUMAV", (await _alias.GetMapAsync("LIPE"))["LUMA"]);
        Assert.Equal(2, await _db.SidFixAliases.CountAsync());
    }

    /// <summary>Gli alias nati quando valevano per tutti restano validi per tutti; lo scalo che ne ha uno suo per
    /// lo stesso prefisso usa il suo.</summary>
    [Fact]
    public async Task Quelli_senza_scalo_valgono_per_tutti_e_il_suo_passa_davanti()
    {
        _db.SidFixAliases.Add(new SidFixAlias { Icao = null, Prefix = "SIV", FixName = "SOSIV" });
        await _db.SaveChangesAsync();
        await _alias.UpsertAsync("LIRN", "SIV", "SIVIL");

        Assert.Equal("SOSIV", (await _alias.GetMapAsync("LIRF"))["SIV"]);
        Assert.Equal("SIVIL", (await _alias.GetMapAsync("LIRN"))["SIV"]);
        Assert.Contains(await _alias.ListAsync(), a => a.Icao is null && a.Prefix == "SIV");
    }
}
