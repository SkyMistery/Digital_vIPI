using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le piste <b>orfane</b>: in archivio ma non più nominate dalla sorgente.
///
/// <para><b>Il caso vero.</b> Il 4 settembre 2026 LIPR (Rimini) mostrava QUATTRO piste — 13, 31, 12, 30 —
/// tutte lunghe 2962 m. IVAO aveva ri-denominato lo scalo per deriva magnetica; il merge era un
/// add-or-update per ident, quindi 12 e 30 erano entrate come piste nuove e 13 e 31 non se n'erano mai
/// andate. Le due morte stavano davanti alle due vive perché le nuove venivano accodate in fondo.</para>
///
/// <para>⚠️ <b>La riga che conta è quella sulla lista vuota.</b> L'import piste è best-effort silenzioso —
/// IVAO 4xx o vuoto ⇒ zero piste, nessun errore — e la stessa lista vuota è come <c>SourceMergeInputs</c>
/// esprime la categoria esclusa dalla policy. Una riconciliazione che leggesse quello zero come «l'aeroporto
/// non ha più piste» svuoterebbe la tabella a ogni sorgente muta. È letteralmente il difetto che azzerò 83
/// poligoni su 83 nelle aree regolamentate.</para>
/// </summary>
public class PisteOrfaneTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirportRepository _airports = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _airports = new EfAirportRepository(_db, new EfMediaMaintenance(_db));

        var structRepo = new EfStructureEditingRepository(_db);
        await structRepo.CreateAccAsync("LIPP", "Padova", "LI");
        await structRepo.CreateAirportAsync("LIPP", "LIPR", "Rimini");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static SourceRunway Rw(string ident, int? len = 2962) => new(ident, len, null, null, null, null);

    private async Task<string[]> IdentiInOrdine() =>
        await _db.AirportRunways.AsNoTracking().OrderBy(r => r.Order).Select(r => r.Ident).ToArrayAsync();

    private async Task ScriviTora(string ident, string tora)
    {
        var r = await _db.AirportRunways.SingleAsync(x => x.Ident == ident);
        r.ToraM = tora;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    // ---- Il caso LIPR --------------------------------------------------------------------------------

    /// <summary>Piste ri-denominate e nessun dato scritto a mano: le vecchie se ne vanno, non si accumulano.</summary>
    [Fact]
    public async Task Piste_ridenominate_senza_lavoro_editoriale_spariscono()
    {
        await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("13"), Rw("31") });

        var esito = await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("12"), Rw("30") });

        Assert.Equal(new[] { "12", "30" }, await IdentiInOrdine());
        Assert.Equal(2, esito.Added);
        Assert.Equal(2, esito.RemovedEmpty);
        Assert.Empty(esito.OrphansWithData);
    }

    /// <summary>
    /// ⚠️ Il cuore della faccenda: la pista morta porta un TORA scritto da una persona, e il merge NON la
    /// cancella. Resta, e viene nominata, perché la tolga chi sa dove spostare quel dato.
    /// </summary>
    [Fact]
    public async Task Una_pista_orfana_con_tora_resta_e_viene_nominata()
    {
        await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("13"), Rw("31") });
        await ScriviTora("13", "2800");

        var esito = await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("12"), Rw("30") });

        Assert.Equal(new[] { "13" }, esito.OrphansWithData);
        Assert.Equal(1, esito.RemovedEmpty);                       // la 31, vuota, se n'è andata
        var pista13 = await _db.AirportRunways.AsNoTracking().SingleAsync(r => r.Ident == "13");
        Assert.Equal("2800", pista13.ToraM);
    }

    /// <summary>Le orfane vanno in CODA: le piste vive si leggono per prime. Prima le nuove si accodavano e
    /// LIPR mostrava 13, 31, 12, 30 — le due morte davanti alle due vive.</summary>
    [Fact]
    public async Task Le_orfane_tenute_finiscono_in_coda_alle_vive()
    {
        await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("13"), Rw("31") });
        await ScriviTora("13", "2800");
        await ScriviTora("31", "2750");

        await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("12"), Rw("30") });

        Assert.Equal(new[] { "12", "30", "13", "31" }, await IdentiInOrdine());
    }

    // ---- La guardia sulla sorgente muta --------------------------------------------------------------

    /// <summary>
    /// ⚠️ Lista vuota = «nessun cambio», MAI «l'aeroporto non ha più piste». È quel che resta di una fetch
    /// andata a vuoto e di una categoria esclusa dalla policy: leggerla come una cancellazione svuoterebbe
    /// la tabella da sola, in silenzio.
    /// </summary>
    [Fact]
    public async Task Una_sorgente_muta_non_cancella_niente()
    {
        await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("13"), Rw("31") });

        var esito = await _airports.MergeFromSourceAsync("LIPR", null, Array.Empty<SourceRunway>());

        Assert.Equal(new[] { "13", "31" }, await IdentiInOrdine());
        Assert.Equal(0, esito.RemovedEmpty);
        Assert.Empty(esito.OrphansWithData);
    }

    /// <summary>La sorgente muta non impedisce alla TA di passare: le due cose sono indipendenti.</summary>
    [Fact]
    public async Task Una_sorgente_muta_lascia_comunque_passare_la_ta()
    {
        await _airports.MergeFromSourceAsync("LIPR", 6000, Array.Empty<SourceRunway>());

        var aeroporto = await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIPR");
        Assert.Equal(6000, aeroporto.TransitionAltitudeFt);
    }

    // ---- Quel che il merge non deve rompere ----------------------------------------------------------

    /// <summary>Le colonne editoriali di una pista che la sorgente ha ancora non si toccano mai.</summary>
    [Fact]
    public async Task Il_merge_non_tocca_le_colonne_editoriali_di_una_pista_viva()
    {
        await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("13") });
        await ScriviTora("13", "2800");

        await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("13", 3000) });

        var pista = await _db.AirportRunways.AsNoTracking().SingleAsync();
        Assert.Equal("2800", pista.ToraM);                          // editoriale intatto
        Assert.Equal(3000, pista.LengthM);                          // sorgente aggiornata
    }

    /// <summary>La stessa testata due volte nella risposta non genera un doppione.</summary>
    [Fact]
    public async Task Una_sorgente_che_ripete_una_testata_non_crea_doppioni()
    {
        var esito = await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("12"), Rw("12"), Rw("30") });

        Assert.Equal(new[] { "12", "30" }, await IdentiInOrdine());
        Assert.Equal(2, esito.Added);
    }

    /// <summary>Ident sporco in archivio (spazi, minuscole): si aggancia lo stesso e si normalizza, invece di
    /// diventare un'orfana e lasciare entrare un doppione accanto.</summary>
    [Fact]
    public async Task Un_ident_sporco_in_archivio_si_aggancia_e_si_normalizza()
    {
        var idAeroporto = await _db.Airports.Where(a => a.Icao == "LIPR").Select(a => a.Id).SingleAsync();
        _db.AirportRunways.Add(new Domain.Entities.AirportRunway { AirportId = idAeroporto, Order = 0, Ident = " 12l ", ToraM = "2800" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var esito = await _airports.MergeFromSourceAsync("LIPR", null, new[] { Rw("12L") });

        Assert.Equal(new[] { "12L" }, await IdentiInOrdine());
        Assert.Empty(esito.OrphansWithData);
        Assert.Equal(1, esito.Updated);
        Assert.Equal("2800", (await _db.AirportRunways.AsNoTracking().SingleAsync()).ToraM);
    }
}
