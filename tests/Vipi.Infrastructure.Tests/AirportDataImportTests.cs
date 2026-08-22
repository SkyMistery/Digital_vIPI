using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il giro periodico di <b>TA e piste</b> (<see cref="AirportDataImportUseCase"/>), dal 22 agosto 2026.
///
/// <para>Prima esisteva solo il percorso a mano (reimport nell'editor, massivo, «Genera documenti»): una TA
/// cambiata in AIRAC restava vecchia finché qualcuno non premeva un bottone. Questi test presidiano le tre
/// cose che il giro automatico non deve sbagliare: <b>rispettare la policy categoria per categoria</b> —
/// senza nemmeno interrogare la sorgente per ciò che non potrebbe scrivere —, <b>non fermarsi</b> al primo
/// aeroporto che dà errore, e <b>non fingere di aver importato</b> quando la sorgente è muta per tutti.</para>
/// </summary>
public class AirportDataImportTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfImportPolicyStore _policy = default!;
    private EfAirportRepository _airports = default!;
    private EfAirportSectorRepository _sectors = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _policy = new EfImportPolicyStore(_db);
        _airports = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        _sectors = new EfAirportSectorRepository(_db);

        var structRepo = new EfStructureEditingRepository(_db);
        await structRepo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await structRepo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        await structRepo.CreateAirportAsync("LIRR", "LIRA", "Roma Ciampino");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Il_giro_aggiorna_TA_e_piste_di_tutti_gli_aeroporti()
    {
        var dir = new FakeDirectory
        {
            Airports =
            {
                new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, 6000),
                new SourceAirport("LIRA", "Roma Ciampino", "LIRR", null, 5000),
            },
        };
        var det = new FakeDetails { Runways = { new SourceRunway("16L", 3902, 160) } };

        var r = await Uso(dir, det).RunAsync();

        Assert.False(r.Skipped);
        Assert.Equal(2, r.Airports);
        Assert.Empty(r.Failures);
        Assert.Equal(6000, (await _airports.LoadAsync("LIRF"))!.TransitionAltitudeFt);
        Assert.Equal(5000, (await _airports.LoadAsync("LIRA"))!.TransitionAltitudeFt);
        Assert.Equal(3902, Assert.Single((await _airports.LoadAsync("LIRF"))!.Runways).LengthM);
    }

    /// <summary>
    /// ⚠️ Non basta «non scrivere»: con entrambe le categorie escluse la sorgente non si <b>interroga</b>,
    /// come già fanno il giro dei Settori e quello delle Aree. Un giro che scarica novantadue volte al
    /// giorno un dato che per scelta non può salvare è traffico che nessuno ha chiesto.
    /// </summary>
    [Fact]
    public async Task Con_entrambe_le_categorie_escluse_la_sorgente_non_si_interroga()
    {
        await _policy.SaveAsync(new ImportPolicySnapshot(TransitionAltitude: false, Runways: false, true, true, true), 1);
        var dir = new FakeDirectory { Airports = { new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, 6000) } };
        var det = new FakeDetails { Runways = { new SourceRunway("16L", 3902, 160) } };

        var r = await Uso(dir, det).RunAsync();

        Assert.True(r.Skipped);
        Assert.Equal(0, r.Airports);
        Assert.Equal(0, dir.Calls);
        Assert.Equal(0, det.RunwayCalls);
        Assert.Null((await _airports.LoadAsync("LIRF"))!.TransitionAltitudeFt);
    }

    /// <summary>
    /// La chiave di stato è una sola per due categorie: è corretta finché il <b>gate</b> resta per categoria.
    /// Qui si presidia proprio quello — escludere le Piste non spegne la TA, e viceversa.
    /// </summary>
    [Fact]
    public async Task Escludere_le_piste_non_spegne_la_TA()
    {
        await _airports.MergeFromSourceAsync("LIRF", null, new[] { ("16L", (int?)3902, (int?)160) });
        await _policy.SaveAsync(new ImportPolicySnapshot(TransitionAltitude: true, Runways: false, true, true, true), 1);

        var dir = new FakeDirectory { Airports = { new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, 6000) } };
        var det = new FakeDetails { Runways = { new SourceRunway("16L", 9999, 160), new SourceRunway("16R", 3900, 160) } };

        await Uso(dir, det).RunAsync();

        var data = await _airports.LoadAsync("LIRF");
        Assert.Equal(6000, data!.TransitionAltitudeFt);        // TA importata: arriva
        Assert.Equal(0, det.RunwayCalls);                      // Piste escluse: nemmeno la fetch
        var pista = Assert.Single(data.Runways);               // la 16R non rientra
        Assert.Equal(3902, pista.LengthM);                     // e la 16L non cambia misura
    }

    /// <summary>
    /// Un 404 su un aeroporto è un fatto locale: gli altri novantuno devono aggiornarsi lo stesso. È la
    /// lezione dell'import SID, dove un fallimento per-aeroporto fermava il resto del giro.
    /// </summary>
    [Fact]
    public async Task Un_aeroporto_che_fallisce_non_ferma_gli_altri()
    {
        var dir = new FakeDirectory
        {
            Airports =
            {
                new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, 6000),
                new SourceAirport("LIRA", "Roma Ciampino", "LIRR", null, 5000),
            },
        };
        var det = new FakeDetails { Runways = { new SourceRunway("16L", 3902, 160) }, FailFor = "LIRA" };

        var r = await Uso(dir, det).RunAsync();

        Assert.Equal(1, r.Airports);
        Assert.Equal("LIRA", Assert.Single(r.Failures).Icao);
        Assert.Equal(6000, (await _airports.LoadAsync("LIRF"))!.TransitionAltitudeFt);
    }

    /// <summary>
    /// ⚠️ Tutti falliti non è «riuscito con zero»: <c>GatedImportLoop</c> marca il successo quando il run non
    /// lancia, e la pagina Sorgenti mostrerebbe verde e data di oggi per un giro che non ha importato niente.
    /// L'eccezione risale, e risale <b>col suo tipo</b>: è così che il chiamante distingue «credenziali
    /// assenti» da un guasto da ritentare.
    /// </summary>
    [Fact]
    public async Task Se_falliscono_tutti_l_errore_risale_col_suo_tipo()
    {
        var det = new FakeDetails { FailFor = "*" };

        await Assert.ThrowsAsync<HttpRequestException>(() => Uso(new FakeDirectory(), det).RunAsync());
    }

    private AirportDataImportUseCase Uso(FakeDirectory dir, FakeDetails det) =>
        new(_sectors, _airports, dir, det, _policy);

    private sealed class FakeDirectory : IAirportDirectory
    {
        public List<SourceAirport> Airports { get; } = new();
        public int Calls;

        public Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult((IReadOnlyList<SourceAirport>)Airports);
        }

        public Task<SourceAirport?> GetByIcaoAsync(string icao, CancellationToken ct = default)
            => Task.FromResult(Airports.FirstOrDefault(a => string.Equals(a.Icao, icao, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class FakeDetails : IAirportDetailProvider
    {
        public List<SourceRunway> Runways { get; } = new();
        public int RunwayCalls;

        /// <summary>ICAO che deve fallire, o <c>*</c> per farli fallire tutti (sorgente giù).</summary>
        public string? FailFor { get; set; }

        public Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<SourceAtcPosition>)Array.Empty<SourceAtcPosition>());

        public Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default)
            => Task.FromResult<SourceAtcPosition?>(null);

        public Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default)
        {
            RunwayCalls++;
            if (FailFor == "*" || string.Equals(FailFor, icao, StringComparison.OrdinalIgnoreCase))
                throw new HttpRequestException($"IVAO 503 su /v2/airports/{icao}/runways");
            return Task.FromResult((IReadOnlyList<SourceRunway>)Runways);
        }
    }
}
