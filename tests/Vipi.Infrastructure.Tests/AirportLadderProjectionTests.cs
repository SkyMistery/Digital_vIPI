using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Gerarchia delle posizioni d'aeroporto (2026-07-31). L'admin imposta il padre sul nodo AEROPORTO in
/// `/vsop/admin/sectorstructure` (<c>Airport.ParentCallsign</c>), ma la proiezione leggeva solo
/// <c>AirportSector.ParentCallsign</c>, popolato per i soli APP: torri, ground e delivery restavano orfani
/// nonostante il padre fosse configurato. Qui si fissa la regola scelta: scaletta interna
/// <b>DEL → GND → TWR → APP</b>, e in cima si esce sul padre dell'aeroporto.
/// </summary>
public class AirportLadderProjectionTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorProjectionService _proj = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(lirr);
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR" });

        // Aeroporto COMPLETO: la scaletta esiste tutta. Il padre dell'aeroporto è il CTR.
        _db.Airports.Add(new Airport { Icao = "LIRF", Name = "Fiumicino", Acc = lirr, ParentCallsign = "LIRR_NE_CTR" });
        _db.AirportSectors.AddRange(
            new AirportSector { ComposePosition = "LIRF_APP", AirportIcao = "LIRF", AccCode = "LIRR", Position = "APP", ParentCallsign = "LIRR_NE_CTR" },
            new AirportSector { ComposePosition = "LIRF_TWR", AirportIcao = "LIRF", AccCode = "LIRR", Position = "TWR" },
            new AirportSector { ComposePosition = "LIRF_GND", AirportIcao = "LIRF", AccCode = "LIRR", Position = "GND" },
            new AirportSector { ComposePosition = "LIRF_DEL", AirportIcao = "LIRF", AccCode = "LIRR", Position = "DEL" },
            new AirportSector { ComposePosition = "LIRF_ATIS", AirportIcao = "LIRF", AccCode = "LIRR", Position = "ATIS" });

        // Aeroporto SENZA APP: la torre è in cima alla scaletta ed esce sul padre dell'aeroporto.
        _db.Airports.Add(new Airport { Icao = "LIRL", Name = "Latina", Acc = lirr, ParentCallsign = "LIRR_NE_CTR" });
        _db.AirportSectors.AddRange(
            new AirportSector { ComposePosition = "LIRL_TWR", AirportIcao = "LIRL", AccCode = "LIRR", Position = "TWR" },
            new AirportSector { ComposePosition = "LIRL_GND", AirportIcao = "LIRL", AccCode = "LIRR", Position = "GND" });

        // Aeroporto senza padre configurato: resta orfano, non si inventa una gerarchia.
        _db.Airports.Add(new Airport { Icao = "LIRU", Name = "Urbe", Acc = lirr });
        _db.AirportSectors.Add(new AirportSector { ComposePosition = "LIRU_TWR", AirportIcao = "LIRU", AccCode = "LIRR", Position = "TWR" });

        await _db.SaveChangesAsync();
        _proj = new EfSectorProjectionService(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<string?> ParentOf(string callsign)
    {
        var s = await _db.Sectors.AsNoTracking().FirstAsync(x => x.Callsign == callsign);
        if (s.ParentSectorId is not int pid) return null;
        return (await _db.Sectors.AsNoTracking().FirstAsync(x => x.Id == pid)).Callsign;
    }

    [Fact]
    public async Task La_scaletta_interna_sale_DEL_GND_TWR_APP()
    {
        await _proj.SyncFromCatalogsAsync();

        Assert.Equal("LIRF_GND", await ParentOf("LIRF_DEL"));
        Assert.Equal("LIRF_TWR", await ParentOf("LIRF_GND"));
        Assert.Equal("LIRF_APP", await ParentOf("LIRF_TWR"));
    }

    [Fact]
    public async Task In_cima_alla_scaletta_si_esce_sul_padre_dell_aeroporto()
    {
        await _proj.SyncFromCatalogsAsync();

        // LIRL non ha APP: la torre è la posizione più alta e prende il padre dell'aeroporto.
        Assert.Equal("LIRR_NE_CTR", await ParentOf("LIRL_TWR"));
        Assert.Equal("LIRL_TWR", await ParentOf("LIRL_GND"));
    }

    [Fact]
    public async Task Il_padre_esplicito_del_catalogo_vince_sulla_scaletta()
    {
        await _proj.SyncFromCatalogsAsync();

        // LIRF_APP ha il proprio ParentCallsign: non deve essere riscritto dal padre dell'aeroporto.
        Assert.Equal("LIRR_NE_CTR", await ParentOf("LIRF_APP"));
    }

    [Fact]
    public async Task Senza_padre_configurato_sull_aeroporto_resta_orfano()
    {
        await _proj.SyncFromCatalogsAsync();

        // Nessuna gerarchia inventata: se l'admin non l'ha compilata, la posizione non si aggancia a nulla.
        Assert.Null(await ParentOf("LIRU_TWR"));
    }

    [Fact]
    public async Task Una_posizione_nascosta_non_spezza_la_scaletta()
    {
        // Torre nascosta: il ground deve saltarla e agganciarsi all'avvicinamento, non restare orfano.
        var twr = await _db.AirportSectors.FirstAsync(s => s.ComposePosition == "LIRF_TWR");
        twr.IsHidden = true;
        await _db.SaveChangesAsync();

        await _proj.SyncFromCatalogsAsync();

        Assert.Equal("LIRF_APP", await ParentOf("LIRF_GND"));
    }

    [Fact]
    public async Task L_ATIS_non_entra_nella_scaletta()
    {
        await _proj.SyncFromCatalogsAsync();

        // L'ATIS non è un settore controllato: non è proiettato e non deve comparire come padre di nessuno.
        Assert.False(await _db.Sectors.AnyAsync(s => s.Callsign == "LIRF_ATIS"));
        Assert.Equal("LIRF_APP", await ParentOf("LIRF_TWR"));
    }
}
