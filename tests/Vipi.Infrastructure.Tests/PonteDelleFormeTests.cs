using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <b>Il ponte delle forme</b> — S11 fase A, carta <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c>
/// §4-bis: ogni salvataggio che tocca la forma, le quote o il gate di una riga di catalogo allinea i pezzi di
/// <c>SectorShapeParts</c>, e la passata d'avvio ripara quel che manca.
///
/// <para>⚠️ Il guasto che questi test prendono non si vede a schermo finché le letture restano sulle colonne: si
/// vedrebbe il giorno che passano ai pezzi, su un settore che nessuno guarda. Per questo ogni scenario scrive
/// <b>come scrive il codice vero</b> — l'entità e un <c>SaveChangesAsync</c> — e non chiama il ponte a mano.</para>
/// </summary>
public class PonteDelleFormeTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private const string Anello = "[[14.5,36.5],[16.0,36.5],[16.0,38.0],[14.5,38.0]]";
    private const string AnelloNuovo = "[[14.6,36.6],[16.1,36.6],[16.1,38.1],[14.6,38.1]]";
    private const string Cerchio = "[[12.0,41.0],[12.1,41.0],[12.1,41.1],[12.0,41.1]]";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = Contesto();
        await _db.Database.EnsureCreatedAsync();
        _db.Accs.Add(new Acc { Code = "LIRR", Name = "Roma" });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private VipiDbContext Contesto() =>
        new(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);

    private async Task<AccSector> CtrAsync(string? anello = Anello, int? basso = 0, int? alto = 19_500)
    {
        var s = new AccSector
        {
            ComposePosition = "LIRR_TS_CTR", CenterId = "LIRR", Position = "CTR",
            RegionMapPolygon = anello, LowerLimit = basso, UpperLimit = alto,
        };
        _db.AccSectors.Add(s);
        await _db.SaveChangesAsync();
        return s;
    }

    private Task<List<SectorShapePart>> PezziAsync() =>
        _db.SectorShapeParts.AsNoTracking().OrderBy(p => p.Source).ThenBy(p => p.State).ToListAsync();

    private Task AipAsync(AccSector s) =>
        new EfSectorShapeParts(_db).ReplacePartsAsync(SourceCatalog.Subcenter, s.Id, s.ComposePosition,
            ShapeSource.Aip, ShapePartState.InForce,
            new[] { new ShapePart(Cerchio, null, 10_500, AirspaceDatum.Gnd, AirspaceDatum.FlightLevel, "GND", "FL105", "ATZ|X") });

    // ---- I salvataggi ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Una_riga_nuova_con_la_forma_nasce_coi_suoi_pezzi_e_le_quote()
    {
        var s = await CtrAsync();

        var p = Assert.Single(await PezziAsync());
        Assert.Equal((SourceCatalog.Subcenter, s.Id, "LIRR_TS_CTR"), (p.Catalog, p.SectorId, p.Callsign));
        Assert.Equal((ShapeSource.Source, ShapePartState.InForce, Anello), (p.Source, p.State, p.PolygonJson));
        Assert.Equal((0, 19_500, "0", "19500"), (p.BaseFeet, p.TopFeet, p.BaseRaw, p.TopRaw));
        Assert.Equal((AirspaceDatum.Amsl, AirspaceDatum.Amsl), (p.BaseDatum, p.TopDatum));
    }

    [Fact]
    public async Task Una_riga_senza_forma_non_ha_pezzi()
    {
        await CtrAsync(anello: null);
        Assert.Empty(await PezziAsync());
    }

    /// <summary>🔴 Le quote cambiate a mano dalla pagina Struttura: col pezzo che le porta dentro, sono una scrittura di forma.</summary>
    [Fact]
    public async Task Le_quote_cambiate_riscrivono_i_pezzi()
    {
        var s = await CtrAsync();

        s.UpperLimit = 24_500;
        await _db.SaveChangesAsync();

        var p = Assert.Single(await PezziAsync());
        Assert.Equal((24_500, "24500"), (p.TopFeet, p.TopRaw));
    }

    [Fact]
    public async Task Un_campo_che_non_tocca_la_forma_non_riscrive_niente()
    {
        var s = await CtrAsync();
        var prima = Assert.Single(await PezziAsync());

        s.IsHidden = true;
        s.Frequency = "127.050";
        await _db.SaveChangesAsync();

        var dopo = Assert.Single(await PezziAsync());
        Assert.Equal((prima.Id, prima.WrittenUtc), (dopo.Id, dopo.WrittenUtc));
    }

    /// <summary>Il sectorfile scrive sopra l'anagrafica: le fonti di catalogo si escludono, e l'ATZ dell'AIP resta.</summary>
    [Fact]
    public async Task Il_sectorfile_prende_il_posto_dell_anagrafica_e_lascia_l_aip()
    {
        var s = await CtrAsync();
        await AipAsync(s);

        s.RegionMapPolygon = AnelloNuovo;
        s.ShapeSource = ShapeSource.Sectorfile;
        await _db.SaveChangesAsync();

        var pezzi = await PezziAsync();
        Assert.Equal(new[] { ShapeSource.Sectorfile, ShapeSource.Aip }.OrderBy(x => x), pezzi.Select(p => p.Source).OrderBy(x => x));
        Assert.Equal(AnelloNuovo, pezzi.Single(p => p.Source == ShapeSource.Sectorfile).PolygonJson);
        Assert.Equal("ATZ|X", pezzi.Single(p => p.Source == ShapeSource.Aip).SourceRef);
    }

    /// <summary>
    /// Il gate AIRAC: il sectorfile disegna il confine del ciclo prossimo. In vigore resta il vecchio, in attesa il
    /// nuovo col ciclo; alla promozione l'attesa se ne va e il nuovo è in vigore.
    /// </summary>
    [Fact]
    public async Task La_forma_differita_diventa_un_insieme_in_attesa_e_la_promozione_lo_chiude()
    {
        var s = await CtrAsync();

        s.RegionMapPolygonInForce = Anello;
        s.RegionMapPolygon = AnelloNuovo;
        s.ShapeAiracCycle = "2611";
        s.ShapeSource = ShapeSource.Sectorfile;
        await _db.SaveChangesAsync();

        var pezzi = await PezziAsync();
        Assert.Equal(2, pezzi.Count);
        var inVigore = pezzi.Single(p => p.State == ShapePartState.InForce);
        var inAttesa = pezzi.Single(p => p.State == ShapePartState.Pending);
        Assert.Equal((Anello, (string?)null), (inVigore.PolygonJson, inVigore.AiracCycle));
        Assert.Equal((AnelloNuovo, "2611", false), (inAttesa.PolygonJson, inAttesa.AiracCycle, inAttesa.ForcePublished));

        s.ShapeForcePublished = true;
        await _db.SaveChangesAsync();
        Assert.True((await PezziAsync()).Single(p => p.State == ShapePartState.Pending).ForcePublished);

        // La promozione, come la fa EfSectorShapeRepository.PromoteDueShapesAsync.
        s.ShapeAiracCycle = null;
        s.RegionMapPolygonInForce = null;
        s.ShapeForcePublished = false;
        await _db.SaveChangesAsync();

        var p = Assert.Single(await PezziAsync());
        Assert.Equal((ShapePartState.InForce, AnelloNuovo), (p.State, p.PolygonJson));
    }

    [Fact]
    public async Task Tolta_la_forma_dalla_colonna_se_ne_vanno_i_pezzi_di_catalogo_ma_non_l_aip()
    {
        var s = await CtrAsync();
        await AipAsync(s);
        Assert.Equal(2, (await PezziAsync()).Count);

        s.RegionMapPolygon = null;
        await _db.SaveChangesAsync();

        Assert.Equal(ShapeSource.Aip, Assert.Single(await PezziAsync()).Source);
    }

    /// <summary>Il risolutore cerca per callsign: un settore rinominato si porta dietro TUTTI i pezzi, AIP compresi.</summary>
    [Fact]
    public async Task Il_callsign_rinominato_si_porta_dietro_tutti_i_pezzi()
    {
        var s = await CtrAsync();
        await AipAsync(s);

        s.ComposePosition = "LIRR_TE_CTR";
        await _db.SaveChangesAsync();

        Assert.All(await PezziAsync(), p => Assert.Equal("LIRR_TE_CTR", p.Callsign));
    }

    [Fact]
    public async Task Un_settore_eliminato_perde_tutti_i_suoi_pezzi()
    {
        var s = await CtrAsync();
        await AipAsync(s);

        _db.AccSectors.Remove(s);
        await _db.SaveChangesAsync();

        Assert.Empty(await PezziAsync());
    }

    [Fact]
    public async Task Il_cerchio_di_ripiego_di_una_torre_e_un_pezzo_sintetico()
    {
        _db.Airports.Add(new Airport { Icao = "LIRU", Name = "Urbe", AccId = (await _db.Accs.SingleAsync()).Id });
        await _db.SaveChangesAsync();
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = "LIRU_TWR", AirportIcao = "LIRU", AccCode = "LIRR", Position = "TWR",
            RegionMapPolygon = Cerchio, IsShapeSynthetic = true,
        });
        await _db.SaveChangesAsync();

        var p = Assert.Single(await PezziAsync());
        Assert.Equal((SourceCatalog.AirportPosition, ShapeSource.Synthetic, "GND", "UNL"), (p.Catalog, p.Source, p.BaseRaw, p.TopRaw));
    }

    // ---- La passata d'avvio e la Diagnostica ----------------------------------------------------------------

    /// <summary>
    /// Il secondo salvataggio del ponte può cadere, e una riga può essere stata scritta da una versione che il ponte
    /// non l'aveva: la passata d'avvio rimette tutto a posto, toglie gli orfani, e al secondo giro non fa niente.
    /// </summary>
    [Fact]
    public async Task La_passata_d_avvio_ripara_e_al_secondo_giro_non_tocca_niente()
    {
        var s = await CtrAsync();
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM SectorShapeParts");
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO SectorShapeParts (Catalog, SectorId, Callsign, Source, State, Ordinal, PolygonJson, BaseDatum, TopDatum, BaseRaw, TopRaw, ForcePublished, WrittenUtc) " +
            "VALUES ('AirportPosition', 999, 'LIXX_TWR', 'Aip', 'InForce', 0, {0}, 'Gnd', 'Amsl', 'GND', 'UNL', 0, '2026-09-16')", Cerchio);
        _db.ChangeTracker.Clear();

        var manutenzione = new EfSectorCatalogMaintenance(_db, new EfImportStateStore(_db));
        Assert.Equal(new[] { "LIRR_TS_CTR", "LIXX_TWR" }, (await manutenzione.ListMisalignedShapePartsAsync()).OrderBy(x => x));

        Assert.Equal(2, await manutenzione.AlignShapePartsAsync());

        var p = Assert.Single(await PezziAsync());
        Assert.Equal((s.Id, ShapeSource.Source), (p.SectorId, p.Source));
        Assert.Empty(await manutenzione.ListMisalignedShapePartsAsync());
        Assert.Equal(0, await manutenzione.AlignShapePartsAsync());
    }

    /// <summary>Un ponte che si dimentica le righe salvate da un contesto NUOVO sarebbe un ponte che vale solo nei test.</summary>
    [Fact]
    public async Task Il_ponte_vale_anche_per_un_contesto_nuovo()
    {
        var s = await CtrAsync();

        await using (var altro = Contesto())
        {
            var riga = await altro.AccSectors.SingleAsync(x => x.Id == s.Id);
            riga.LowerLimit = 5_500;
            await altro.SaveChangesAsync();
        }

        Assert.Equal(5_500, Assert.Single(await PezziAsync()).BaseFeet);
    }

    /// <summary>
    /// 🔴 Visto in produzione il 16 settembre 2026, 20:44Z: due processi (Passenger ne teneva vivo uno della
    /// versione prima) con la stessa scadenza d'import riscrivono INSIEME i pezzi dello stesso settore. Uno
    /// cancella la riga che l'altro ha appena letto, il DELETE dell'altro tocca zero righe, e il
    /// <c>DbUpdateConcurrencyException</c> del ponte faceva cadere l'import — che aveva già salvato le colonne — e
    /// lasciava il contesto sporco a tutti i ripieghi dopo. Il ponte deve rileggere dall'ARCHIVIO (le colonne le
    /// ha scritte per ultimo l'altro) e allineare a quelle, senza far cadere chi ha salvato.
    /// </summary>
    [Fact]
    public async Task Se_un_altro_scrittore_riscrive_i_pezzi_nel_mezzo_il_ponte_riallinea_all_archivio()
    {
        var s = await CtrAsync();
        await _db.DisposeAsync();

        var altroScrittore = new AltroScrittoreNelMezzo(async () =>
        {
            await using var altro = Contesto();
            var riga = await altro.AccSectors.SingleAsync(x => x.Id == s.Id);
            riga.LowerLimit = 5_500;
            await altro.SaveChangesAsync();
        });
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn)
            .AddInterceptors(altroScrittore).Options);

        var mia = await _db.AccSectors.SingleAsync(x => x.Id == s.Id);
        mia.UpperLimit = 24_500;
        await _db.SaveChangesAsync();   // ⚠️ non deve lanciare: le colonne sono già salvate

        Assert.True(altroScrittore.Entrato);
        _db.ChangeTracker.Clear();
        var p = Assert.Single(await PezziAsync());
        Assert.Equal((5_500, 24_500), (p.BaseFeet, p.TopFeet));
        Assert.Empty(await new EfSectorCatalogMaintenance(_db, new EfImportStateStore(_db)).ListMisalignedShapePartsAsync());
    }

    /// <summary>
    /// Se il salvataggio dei pezzi cade OGNI volta, il ponte si arrende dopo tre tentativi: chi ha scritto le
    /// colonne non ne sa niente, i pezzi pendenti non restano nel contesto, la Diagnostica li conta e la passata
    /// d'avvio li rimette a posto.
    /// </summary>
    [Fact]
    public async Task Se_i_pezzi_non_si_salvano_mai_il_salvataggio_regge_e_la_passata_d_avvio_ripara()
    {
        var s = await CtrAsync();
        await _db.DisposeAsync();

        var altroScrittore = new AltroScrittoreNelMezzo(
            () => throw new DbUpdateException("simulato: un altro scrittore ha vinto"), sempre: true);
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn)
            .AddInterceptors(altroScrittore).Options);

        var mia = await _db.AccSectors.SingleAsync(x => x.Id == s.Id);
        mia.UpperLimit = 24_500;
        await _db.SaveChangesAsync();

        Assert.Equal(3, altroScrittore.Volte);
        await using var pulito = Contesto();
        Assert.Equal(24_500, (await pulito.AccSectors.SingleAsync(x => x.Id == s.Id)).UpperLimit);
        Assert.False(pulito.ChangeTracker.HasChanges());
        Assert.False(_db.ChangeTracker.Entries<SectorShapePart>().Any(), "i pezzi del ponte caduto non restano nel contesto");

        var manutenzione = new EfSectorCatalogMaintenance(pulito, new EfImportStateStore(pulito));
        Assert.Equal(new[] { "LIRR_TS_CTR" }, await manutenzione.ListMisalignedShapePartsAsync());
        Assert.Equal(1, await manutenzione.AlignShapePartsAsync());
        Assert.Empty(await manutenzione.ListMisalignedShapePartsAsync());
    }

    /// <summary>Entra nel salvataggio del PONTE (quello che cancella pezzi), prima che parta: una volta, o sempre.</summary>
    private sealed class AltroScrittoreNelMezzo(Func<Task> scrivi, bool sempre = false) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public int Volte { get; private set; }
        public bool Entrato => Volte > 0;

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken ct = default)
        {
            if ((sempre || !Entrato) && eventData.Context!.ChangeTracker.Entries<SectorShapePart>().Any(e => e.State == EntityState.Deleted))
            {
                Volte++;
                await scrivi();
            }
            return result;
        }
    }

    // ---- La precedenza in archivio --------------------------------------------------------------------------

    /// <summary>
    /// 🔴 Rovesciata il 16 settembre 2026: con il sectorfile in archivio accanto all'ATZ automatica, vince il
    /// sectorfile. Con l'ordine di prima l'ATZ lo avrebbe scavalcato — il difetto che §3c aveva già preso una volta.
    /// </summary>
    [Fact]
    public async Task In_archivio_un_confine_del_sectorfile_passa_davanti_all_atz_automatica()
    {
        var s = await CtrAsync();
        await AipAsync(s);
        s.ShapeSource = ShapeSource.Sectorfile;
        await _db.SaveChangesAsync();

        var letti = await new EfSectorShapeParts(_db).ListInForceByCallsignAsync(new[] { "LIRR_TS_CTR" });

        Assert.Equal(ShapeSource.Sectorfile, letti["LIRR_TS_CTR"].Source);
    }
}
