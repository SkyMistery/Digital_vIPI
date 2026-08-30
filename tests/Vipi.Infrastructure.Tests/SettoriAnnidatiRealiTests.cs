using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Padre e figlio in frequenza insieme, con la shape del padre che copre anche quella del figlio: di chi è
/// il traffico che sta solo nel figlio?
///
/// <para>Domanda del committente (24 agosto), e la risposta è <b>del figlio soltanto</b> — un aereo, una
/// sessione. Vale per gli ACC annidati (nel <c>vipi.db</c> reale <c>LIRR_TS_CTR</c> è figlio di
/// <c>LIRR_NE_CTR</c>) e per gli avvicinamenti annidati (21 APP su 64 pendono da un altro APP), perché il
/// meccanismo è lo stesso: la profondità nell'albero è il criterio più forte dopo la fase di volo.</para>
/// </summary>
public class SettoriAnnidatiRealiTests : IAsyncLifetime
{
    // Padre: un quadrato grande. Figlio: un quadrato dentro il primo — la geometria del caso reale.
    private const string Padre = "[[10,40],[16,40],[16,44],[10,44]]";
    private const string Figlio = "[[12,41],[14,41],[14,42],[12,42]]";

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR", RegionMapPolygon = Padre, LowerLimit = 0 });
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIRR_TS_CTR", CenterId = "LIRR", Position = "CTR", RegionMapPolygon = Figlio, LowerLimit = 0 });
        await _db.SaveChangesAsync();

        var ne = new Sector { Callsign = "LIRR_NE_CTR", Name = "NE", AccId = acc.Id, Type = SectorType.Ctr, Kind = SectorKind.Acc };
        _db.Sectors.Add(ne);
        await _db.SaveChangesAsync();

        _db.Sectors.Add(new Sector
        {
            Callsign = "LIRR_TS_CTR", Name = "TS", AccId = acc.Id, Type = SectorType.Ctr,
            Kind = SectorKind.Acc, ParentSectorId = ne.Id,
        });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<IReadOnlyList<SectorClaim>> Claims(params string[] online) =>
        SectorVolumeMap.BuildClaims(
            await new EfSectorVolumeCatalog(_db, new EfSectorShapeResolver(_db, new EfSectorAirspaceBindings(_db), new EfSectorShapeParts(_db))).GetAllAsync(),
            new HashSet<string>(online, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public async Task Col_figlio_in_frequenza_il_traffico_dentro_di_lui_e_SOLO_suo()
    {
        var claims = await Claims("LIRR_NE_CTR", "LIRR_TS_CTR");

        // Il punto sta dentro tutti e due i poligoni: la shape del padre copre anche quella del figlio.
        Assert.True(claims.Single(c => c.Volume.Callsign == "LIRR_NE_CTR").Volume.Contains(41.5, 13, 30_000));
        Assert.True(claims.Single(c => c.Volume.Callsign == "LIRR_TS_CTR").Volume.Contains(41.5, 13, 30_000));

        // Ma l'attribuzione ne sceglie UNO: il più profondo.
        Assert.Equal("LIRR_TS_CTR",
            TrafficAttribution.Attribute(claims, 41.5, 13, 30_000, FlightPhase.Airborne));
    }

    [Fact]
    public async Task Fuori_dal_figlio_ma_dentro_il_padre_il_traffico_e_del_padre()
    {
        var claims = await Claims("LIRR_NE_CTR", "LIRR_TS_CTR");

        Assert.Equal("LIRR_NE_CTR",
            TrafficAttribution.Attribute(claims, 43.5, 15, 30_000, FlightPhase.Airborne));
    }

    [Fact]
    public async Task Senza_il_figlio_in_frequenza_il_padre_si_prende_anche_la_sua_area()
    {
        var claims = await Claims("LIRR_NE_CTR");

        Assert.Equal("LIRR_NE_CTR",
            TrafficAttribution.Attribute(claims, 41.5, 13, 30_000, FlightPhase.Airborne));
    }

    [Fact]
    public async Task Col_solo_figlio_in_frequenza_il_padre_non_conta_niente()
    {
        var claims = await Claims("LIRR_TS_CTR");

        Assert.Equal("LIRR_TS_CTR",
            TrafficAttribution.Attribute(claims, 41.5, 13, 30_000, FlightPhase.Airborne));
        Assert.Null(TrafficAttribution.Attribute(claims, 43.5, 15, 30_000, FlightPhase.Airborne));
    }

    [Fact]
    public async Task Anche_senza_legame_di_parentela_vince_comunque_il_poligono_piu_piccolo()
    {
        // Rete di sicurezza: se un domani la gerarchia non fosse compilata, i due settori sarebbero due
        // radici. L'ordine di scelta scende all'area del bounding box, e il più piccolo è ancora il figlio.
        var ts = await _db.Sectors.SingleAsync(s => s.Callsign == "LIRR_TS_CTR");
        ts.ParentSectorId = null;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var claims = await Claims("LIRR_NE_CTR", "LIRR_TS_CTR");
        Assert.All(claims, c => Assert.Equal(0, c.Depth));

        Assert.Equal("LIRR_TS_CTR",
            TrafficAttribution.Attribute(claims, 41.5, 13, 30_000, FlightPhase.Airborne));
    }
}
