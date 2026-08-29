using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le coordinate delle <b>soglie pista</b> (carta <c>2026-08-27-vsop-militari.md</c> §12c): IVAO le manda con
/// le piste — una riga per soglia — e fino al 30 agosto 2026 di otto campi ne mappavamo quattro.
///
/// <para>
/// ⚠️ <b>Il test che conta è quello sul salvataggio editoriale.</b> <c>SaveRunwaysAsync</c> cancella e
/// riscrive tutte le righe — è l'unico modo di gestire ordine e cancellazioni in un colpo — e le coordinate
/// della soglia non passano dall'editor: senza la conservazione per ident sparirebbero al primo salvataggio
/// di una colonna qualsiasi, e sarebbero tornate solo al re-import successivo. Nessun errore, nessun avviso:
/// una tabella che si svuota da sola.
/// </para>
/// </summary>
public class SogliePisteTests : IAsyncLifetime
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
        await structRepo.CreateAirportAsync("LIPP", "LIPI", "Rivolto");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>I valori sono quelli VERI misurati sul filo il 29 agosto 2026 (<c>/v2/airports/LIPI/runways</c>).</summary>
    private static SourceRunway Rw06(double? lat = 45.9735305556, double? lon = 13.0350638889, int? elev = 162) =>
        new("06", 2555, 57, lat, lon, elev);

    [Fact]
    public async Task Il_merge_porta_dentro_soglia_ed_elevazione()
    {
        await _airports.MergeFromSourceAsync("LIPI", null, new[] { Rw06() });

        var pista = await _db.AirportRunways.AsNoTracking().SingleAsync();
        Assert.Equal(45.9735305556, pista.ThresholdLat!.Value, 9);
        Assert.Equal(13.0350638889, pista.ThresholdLon!.Value, 9);
        Assert.Equal(162, pista.ThresholdElevationFt);
    }

    /// <summary>⚠️ L'assenza non cancella: un giro che non porta le coordinate lascia quelle che ci sono. È
    /// la stessa regola dell'anagrafica radioassistenze, e la stessa che azzerò 83 poligoni su 83.</summary>
    [Fact]
    public async Task Un_giro_senza_coordinate_non_le_cancella()
    {
        await _airports.MergeFromSourceAsync("LIPI", null, new[] { Rw06() });

        await _airports.MergeFromSourceAsync("LIPI", null, new[] { Rw06(lat: null, lon: null, elev: null) });

        var pista = await _db.AirportRunways.AsNoTracking().SingleAsync();
        Assert.NotNull(pista.ThresholdLat);
        Assert.Equal(162, pista.ThresholdElevationFt);
    }

    /// <summary>Mezza coppia non è una posizione: o arrivano tutt'e due, o non si scrive niente.</summary>
    [Fact]
    public async Task Mezza_coordinata_non_si_salva()
    {
        await _airports.MergeFromSourceAsync("LIPI", null, new[] { Rw06(lon: null) });

        var pista = await _db.AirportRunways.AsNoTracking().SingleAsync();
        Assert.Null(pista.ThresholdLat);
        Assert.Null(pista.ThresholdLon);
    }

    /// <summary>
    /// ⚠️ Il difetto vero: salvare una colonna editoriale non deve portarsi via le coordinate. Qui si salva
    /// solo «Patterns», e la soglia dev'essere ancora lì.
    /// </summary>
    [Fact]
    public async Task Salvare_una_colonna_editoriale_non_perde_la_soglia()
    {
        await _airports.MergeFromSourceAsync("LIPI", null, new[] { Rw06() });
        var prima = (await _airports.LoadAsync("LIPI"))!.Runways.Single();

        await _airports.SaveRunwaysAsync("LIPI", new[] { prima with { Patterns = "circuito a sinistra" } });

        var dopo = (await _airports.LoadAsync("LIPI"))!.Runways.Single();
        Assert.Equal("circuito a sinistra", dopo.Patterns);
        Assert.Equal(45.9735305556, dopo.ThresholdLat!.Value, 9);
        Assert.Equal(162, dopo.ThresholdElevationFt);
    }

    /// <summary>
    /// E le riporta anche a chi <b>non le passa affatto</b>: la conservazione sta nel repository e non nella
    /// buona memoria del chiamante — un editor che costruisce le righe da zero non deve poterle perdere.
    /// </summary>
    [Fact]
    public async Task Le_soglie_si_riportano_anche_se_il_chiamante_non_le_passa()
    {
        await _airports.MergeFromSourceAsync("LIPI", null, new[] { Rw06() });
        var prima = (await _airports.LoadAsync("LIPI"))!.Runways.Single();

        // Una riga ricostruita a mano, senza i campi di sorgente: è quel che farebbe un editor distratto.
        await _airports.SaveRunwaysAsync("LIPI", new[]
        {
            new RunwayRow(prima.Id, prima.Ident, prima.LengthM, prima.Bearing, null, null, null, null, null),
        });

        Assert.NotNull((await _airports.LoadAsync("LIPI"))!.Runways.Single().ThresholdLat);
    }

    /// <summary>Una pista rinominata perde la sua soglia, ed è giusto: la conservazione è per IDENT, e con un
    /// altro ident quella non è più la stessa testata. Al re-import torna quella vera.</summary>
    [Fact]
    public async Task Una_pista_rinominata_non_si_porta_dietro_la_soglia_di_un_altra()
    {
        await _airports.MergeFromSourceAsync("LIPI", null, new[] { Rw06() });
        var prima = (await _airports.LoadAsync("LIPI"))!.Runways.Single();

        await _airports.SaveRunwaysAsync("LIPI", new[] { prima with { Ident = "24" } });

        Assert.Null((await _airports.LoadAsync("LIPI"))!.Runways.Single().ThresholdLat);
    }

    /// <summary>
    /// La proiezione scrive la soglia già in <b>sessagesimale</b>, e lo fa nel modello di vista perché quella
    /// riga finisce negli snapshot di release: una release fotografa quel che si legge, non due numeri da
    /// ri-formattare al view.
    /// </summary>
    [Fact]
    public async Task La_vista_porta_la_soglia_gia_scritta_in_sessagesimale()
    {
        await _airports.MergeFromSourceAsync("LIPI", null, new[] { Rw06() });

        var vista = AirportSectionProjection.Runways(await _airports.LoadAsync("LIPI"));

        Assert.Equal("N45°58'24.71''E013°02'06.23''", vista.Rows.Single().Threshold);
        Assert.Equal(162, vista.Rows.Single().ThresholdElevationFt);
    }

    /// <summary>Senza coordinate la cella è vuota — non «0°0'0''», che sarebbe un punto in mezzo all'oceano
    /// stampato con l'aria di essere un dato.</summary>
    [Fact]
    public async Task Senza_coordinate_la_cella_resta_vuota()
    {
        await _airports.MergeFromSourceAsync("LIPI", null, new[] { new SourceRunway("06", 2555, 57) });

        var vista = AirportSectionProjection.Runways(await _airports.LoadAsync("LIPI"));

        Assert.Equal("", vista.Rows.Single().Threshold);
        Assert.Null(vista.Rows.Single().ThresholdElevationFt);
    }
}
