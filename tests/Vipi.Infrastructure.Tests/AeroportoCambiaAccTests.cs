using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Un aeroporto che cambia ACC. IVAO può spostarlo da un centro all'altro; da noi lo sposta una PERSONA
/// (<c>MoveAirportAsync</c>), perché l'import è additivo e non riassegna mai un ICAO già in archivio.
///
/// <para>⚠️ Lo spostamento deve arrivare fino al <b>catalogo</b>. I <c>Sector</c> sono una proiezione: la
/// fonte è <c>AirportSector</c>, e se lì resta il vecchio codice ACC la prima riproiezione — il giro
/// notturno, o un qualunque salvataggio che la scatena — riporta i settori indietro. Misurato il 5 settembre
/// 2026 guidando l'app: aeroporto e settori proiettati su LIRR, righe di catalogo ancora su LIBB.</para>
/// </summary>
public class AeroportoCambiaAccTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorProjectionService _proj = default!;
    private EfStructureEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var libb = new Acc { Code = "LIBB", Name = "Brindisi", CountryPrefix = "LI" };
        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.AddRange(libb, lirr);
        _db.AccSectors.AddRange(
            new AccSector { ComposePosition = "LIBB_ES_CTR", CenterId = "LIBB", Position = "CTR" },
            new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR" });

        _db.Airports.Add(new Airport { Icao = "LIBD", Name = "Bari", Acc = libb, ParentCallsign = "LIBB_ES_CTR" });
        _db.AirportSectors.AddRange(
            new AirportSector { ComposePosition = "LIBD_CS0_APP", AirportIcao = "LIBD", AccCode = "LIBB", Position = "APP", ParentCallsign = "LIBB_ES_CTR" },
            new AirportSector { ComposePosition = "LIBD_TWR", AirportIcao = "LIBD", AccCode = "LIBB", Position = "TWR" });

        await _db.SaveChangesAsync();
        _proj = new EfSectorProjectionService(_db);
        _repo = new EfStructureEditingRepository(_db);
        await _proj.SyncFromCatalogsAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<string?> AccDelSettore(string callsign)
    {
        var s = await _db.Sectors.AsNoTracking().FirstOrDefaultAsync(x => x.Callsign == callsign);
        if (s is null) return null;
        return (await _db.Accs.AsNoTracking().FirstAsync(a => a.Id == s.AccId)).Code;
    }

    private async Task<string> AccDelCatalogo(string callsign) =>
        (await _db.AirportSectors.AsNoTracking().FirstAsync(x => x.ComposePosition == callsign)).AccCode;

    private async Task<int> AirportId() =>
        (await _db.Airports.AsNoTracking().FirstAsync(a => a.Icao == "LIBD")).Id;

    [Fact]
    public async Task Lo_spostamento_porta_con_se_anagrafica_catalogo_e_proiezione()
    {
        Assert.Equal("LIBB", await AccDelSettore("LIBD_TWR"));

        await _repo.MoveAirportAsync(await AirportId(), "LIRR");

        Assert.Equal("LIRR", (await _db.Accs.AsNoTracking()
            .FirstAsync(a => a.Id == _db.Airports.AsNoTracking().First(x => x.Icao == "LIBD").AccId)).Code);
        Assert.Equal("LIRR", await AccDelSettore("LIBD_TWR"));
        Assert.Equal("LIRR", await AccDelSettore("LIBD_CS0_APP"));

        // ⚠️ Il pezzo che mancava: la FONTE. Senza, quel che segue non regge.
        Assert.Equal("LIRR", await AccDelCatalogo("LIBD_TWR"));
        Assert.Equal("LIRR", await AccDelCatalogo("LIBD_CS0_APP"));
    }

    [Fact]
    public async Task La_riproiezione_non_riporta_indietro_i_settori()
    {
        await _repo.MoveAirportAsync(await AirportId(), "LIRR");

        // Il giro notturno, o un qualunque salvataggio che la scatena.
        await _proj.SyncFromCatalogsAsync();

        Assert.Equal("LIRR", await AccDelSettore("LIBD_TWR"));
        Assert.Equal("LIRR", await AccDelSettore("LIBD_CS0_APP"));
    }

    /// <summary>
    /// Il padre che resta fuori ACC si stacca: un APP d'aeroporto non pende più dal CTR del centro che
    /// l'aeroporto ha lasciato. Chi lo riaggancia è una persona, nell'editor della struttura.
    /// </summary>
    [Fact]
    public async Task Il_padre_rimasto_nell_ACC_di_prima_si_stacca()
    {
        await _repo.MoveAirportAsync(await AirportId(), "LIRR");
        await _proj.SyncFromCatalogsAsync();

        var app = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIBD_CS0_APP");
        Assert.Null(app.ParentSectorId);
    }
}
