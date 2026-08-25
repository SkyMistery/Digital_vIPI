using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il ponte fra il legame vecchio e quello nuovo (25 agosto 2026).
///
/// <para>Fino a quel giorno la vIPI d'aeroporto si trovava passando dai settori: una TWR o una GND con
/// <c>DocumentId</c>. Ora il legame autoritativo è <c>Airport.DocumentId</c>, e i documenti già scritti devono
/// arrivarci da soli — altrimenti la strada nuova li vede come inesistenti e l'editor ne crea di nuovi accanto
/// a quelli buoni.</para>
///
/// <para>⚠️ La trappola che questi test presidiano è l'APP non remotizzato: è anch'esso un settore
/// <c>Kind=Airport</c> con l'ICAO dell'aeroporto, ma ha un documento tutto suo. Collegando l'aeroporto a
/// QUELLO, lo scalo mostrerebbe il documento dell'avvicinamento al posto del proprio.</para>
/// </summary>
public class LinkAirportDocumentsTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentMaintenance _manutenzione = default!;
    private Acc _acc = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _manutenzione = new EfDocumentMaintenance(_db);
        _acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(_acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task<int> NuovoDocumentoAsync(string titolo)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = titolo, Language = Language.It,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    /// <summary>Un aeroporto nella forma VECCHIA: il documento sta sui settori, l'aeroporto non lo sa.</summary>
    private async Task<Airport> ScaloStoricoAsync(string icao, params (string Callsign, SectorType Type, ApproachKind? App, int? DocId, bool Primary)[] settori)
    {
        var apt = new Airport { Icao = icao, Name = icao, Acc = _acc };
        _db.Airports.Add(apt);
        await _db.SaveChangesAsync();
        foreach (var s in settori)
            _db.Sectors.Add(new Sector
            {
                Acc = _acc, Callsign = s.Callsign, Name = s.Callsign, Type = s.Type, Kind = SectorKind.Airport,
                ApproachKind = s.App, AirportId = apt.Id, AirportIcao = icao, IsActive = true,
                DocumentId = s.DocId, IsPrimary = s.Primary,
            });
        await _db.SaveChangesAsync();
        return apt;
    }

    [Fact]
    public async Task Un_aeroporto_con_la_torre_arriva_al_suo_documento()
    {
        var doc = await NuovoDocumentoAsync("vIPI — LIRA Ciampino");
        var apt = await ScaloStoricoAsync("LIRA", ("LIRA_TWR", SectorType.Twr, null, doc, true));

        Assert.Equal(1, await _manutenzione.LinkAirportDocumentsAsync());

        await _db.Entry(apt).ReloadAsync();
        Assert.Equal(doc, apt.DocumentId);
    }

    [Fact]
    public async Task Il_documento_dell_app_non_remotizzato_non_diventa_quello_dell_aeroporto()
    {
        var docApp = await NuovoDocumentoAsync("vIPI — LIRP_APP Pisa Approach");
        var docScalo = await NuovoDocumentoAsync("vIPI — LIRP Pisa");
        // L'APP nasce PRIMA, come nei dati veri: chi prendesse il primo che capita prenderebbe lui.
        var apt = await ScaloStoricoAsync("LIRP",
            ("LIRP_APP", SectorType.App, ApproachKind.Standalone, docApp, true),
            ("LIRP_TWR", SectorType.Twr, null, docScalo, true));

        await _manutenzione.LinkAirportDocumentsAsync();

        await _db.Entry(apt).ReloadAsync();
        Assert.Equal(docScalo, apt.DocumentId);
        Assert.NotEqual(docApp, apt.DocumentId);
    }

    [Fact]
    public async Task Uno_scalo_col_solo_app_non_remotizzato_resta_senza_documento_daeroporto()
    {
        // È il caso di LIBG (Taranto Grottaglie): la sorgente gli dà solo un APP non remotizzato. Quel documento
        // descrive l'avvicinamento, non lo scalo, e collegarlo qui sarebbe una bugia. Lo scalo resta scollegato
        // finché qualcuno non gli genera la SUA vIPI — che ora è possibile, perché non serve più una torre.
        var docApp = await NuovoDocumentoAsync("vIPI — LIBG_APP");
        var apt = await ScaloStoricoAsync("LIBG", ("LIBG_APP", SectorType.App, ApproachKind.Standalone, docApp, true));

        Assert.Equal(0, await _manutenzione.LinkAirportDocumentsAsync());

        await _db.Entry(apt).ReloadAsync();
        Assert.Null(apt.DocumentId);
    }

    [Fact]
    public async Task Rigirarla_non_cambia_niente()
    {
        var doc = await NuovoDocumentoAsync("vIPI — LIRA Ciampino");
        await ScaloStoricoAsync("LIRA", ("LIRA_TWR", SectorType.Twr, null, doc, true));

        Assert.Equal(1, await _manutenzione.LinkAirportDocumentsAsync());
        // Idempotente: al secondo giro non c'è più niente da collegare. Gira a ogni avvio.
        Assert.Equal(0, await _manutenzione.LinkAirportDocumentsAsync());
    }

    [Fact]
    public async Task Una_scelta_gia_fatta_non_viene_riscritta()
    {
        var vecchio = await NuovoDocumentoAsync("vIPI — LIRA (vecchio)");
        var buono = await NuovoDocumentoAsync("vIPI — LIRA (quello giusto)");
        var apt = await ScaloStoricoAsync("LIRA", ("LIRA_TWR", SectorType.Twr, null, vecchio, true));
        apt.DocumentId = buono;
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _manutenzione.LinkAirportDocumentsAsync());

        await _db.Entry(apt).ReloadAsync();
        Assert.Equal(buono, apt.DocumentId);
    }
}
