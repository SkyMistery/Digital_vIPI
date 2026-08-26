using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Quali file di settore leggere lo dice <c>ITALY.isc</c>, l'indice che carica Aurora stessa. Le righe
/// provate sono copiate da quello vero del 26 agosto 2026.
/// </summary>
public class SectorShapeIndiceTests
{
    private const string Isc = """
        [INFO]
        ITALY
        [ATC]
        F;DYNAMIC_SEC\GCI.tfl
        F;DYNAMIC_SEC\limmctr.tfl
        F;DYNAMIC_SEC\lirrctr.tfl
        F;DYNAMIC_SEC\lirrctr.tfl
        F;DYNAMIC_SEC\twrs.tfl
        F;NAVAIDS\itfix.fix
        [GEO]
        """;

    [Fact]
    public void Prende_i_file_di_settore_dall_indice() =>
        Assert.Equal(
            new[] { "DYNAMIC_SEC/GCI.tfl", "DYNAMIC_SEC/limmctr.tfl", "DYNAMIC_SEC/lirrctr.tfl" },
            AuroraSectorShapeProvider.FileDiSettore(Isc));

    /// <summary>⚠️ La ripetizione c'è davvero: <c>ITALY.isc</c> cita <c>lirrctr.tfl</c> due volte di seguito.
    /// Leggerlo due volte non romperebbe niente, ma è una richiesta di rete per niente.</summary>
    [Fact]
    public void Un_file_citato_due_volte_si_legge_una_volta_sola() =>
        Assert.Single(AuroraSectorShapeProvider.FileDiSettore(Isc), f => f.EndsWith("lirrctr.tfl"));

    /// <summary>Le TWR hanno il loro provider: leggerle di qua vorrebbe dire due strade che scrivono la
    /// stessa colonna con regole diverse.</summary>
    [Fact]
    public void Le_torri_restano_al_loro_provider() =>
        Assert.DoesNotContain(AuroraSectorShapeProvider.FileDiSettore(Isc), f => f.Contains("twrs"));

    [Fact]
    public void Gli_altri_include_non_entrano() =>
        Assert.DoesNotContain(AuroraSectorShapeProvider.FileDiSettore(Isc), f => f.Contains("NAVAIDS"));

    [Fact]
    public void Un_indice_vuoto_non_da_niente() =>
        Assert.Empty(AuroraSectorShapeProvider.FileDiSettore(""));
}

/// <summary>Il repository del ripiego shape, contro un database vero.</summary>
public class SectorShapeRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorShapeRepository _repo = default!;

    private const string Quadrato = "[[11.0,44.0],[11.5,44.0],[11.5,44.5],[11.0,44.5]]";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRF", Name = "Fiumicino", Acc = acc });
        await _db.SaveChangesAsync();

        _db.AccSectors.AddRange(
            new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR" },
            new AccSector { ComposePosition = "LIRR_FSS", CenterId = "LIRR", Position = "FSS", RegionMapPolygon = Quadrato },
            new AccSector { ComposePosition = "LIRR_VUOTO_CTR", CenterId = "LIRR", Position = "CTR", RegionMapPolygon = "[]" });
        _db.AirportSectors.AddRange(
            new AirportSector { ComposePosition = "LIRF_TW1_APP", AirportIcao = "LIRF", AccCode = "LIRR", Position = "APP" },
            new AirportSector { ComposePosition = "LIRF_TWR", AirportIcao = "LIRF", AccCode = "LIRR", Position = "TWR" },
            new AirportSector { ComposePosition = "LIRF_GND", AirportIcao = "LIRF", AccCode = "LIRR", Position = "GND" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _repo = new EfSectorShapeRepository(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Elenca_solo_le_posizioni_che_hanno_un_area()
    {
        var righe = await _repo.ListShapeCandidatesAsync();

        Assert.Equal(
            new[] { "LIRF_TW1_APP", "LIRR_FSS", "LIRR_NE_CTR", "LIRR_VUOTO_CTR" },
            righe.Select(r => r.Callsign).OrderBy(c => c));
    }

    /// <summary>⚠️ Le TWR restano fuori: hanno il loro ripiego, e due strade sulla stessa colonna divergono.</summary>
    [Fact]
    public async Task Le_torri_e_i_ground_restano_fuori()
    {
        var righe = await _repo.ListShapeCandidatesAsync();

        Assert.DoesNotContain(righe, r => r.Callsign is "LIRF_TWR" or "LIRF_GND");
    }

    [Fact]
    public async Task Dice_chi_ha_gia_un_area_che_si_disegna()
    {
        var righe = (await _repo.ListShapeCandidatesAsync()).ToDictionary(r => r.Callsign);

        Assert.True(righe["LIRR_FSS"].HasUsableShape);
        Assert.False(righe["LIRR_NE_CTR"].HasUsableShape);     // null
        Assert.False(righe["LIRR_VUOTO_CTR"].HasUsableShape);  // "[]"
    }

    [Fact]
    public async Task Scrive_la_shape_sul_catalogo_ACC()
    {
        var riga = (await _repo.ListShapeCandidatesAsync()).Single(r => r.Callsign == "LIRR_NE_CTR");

        await _repo.SetShapeAsync(SourceCatalog.Subcenter, riga.Id, Quadrato);

        Assert.Equal(Quadrato, (await _db.AccSectors.AsNoTracking()
            .SingleAsync(x => x.ComposePosition == "LIRR_NE_CTR")).RegionMapPolygon);
    }

    /// <summary>
    /// ⚠️ Un poligono del sectorfile <b>non</b> è una shape sintetica: è disegnato da una persona. Marcarlo
    /// tale farebbe credere ai ripieghi TWR di poterlo sostituire con un cerchio.
    /// </summary>
    [Fact]
    public async Task La_shape_dal_sectorfile_non_e_sintetica()
    {
        var riga = (await _repo.ListShapeCandidatesAsync()).Single(r => r.Callsign == "LIRF_TW1_APP");

        await _repo.SetShapeAsync(SourceCatalog.AirportPosition, riga.Id, Quadrato);

        var dopo = await _db.AirportSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIRF_TW1_APP");
        Assert.Equal(Quadrato, dopo.RegionMapPolygon);
        Assert.False(dopo.IsShapeSynthetic);
    }
}
