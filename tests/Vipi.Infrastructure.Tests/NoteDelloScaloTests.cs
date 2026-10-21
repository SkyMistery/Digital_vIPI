using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le note dello scalo diventano paragrafi (S123, 21 ottobre 2026, committente: le note «devono sparire, dove voglio
/// scrivere testo seleziono paragrafo o quello che è e lo metto»): la nota delle LVP nella sezione «LVP», quelle delle
/// regole piste in un paragrafo della sezione «Regole piste», una riga per regola.
/// </summary>
public class NoteDelloScaloTests : IAsyncLifetime
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

    /// <summary>Un documento con una versione e le due sezioni, col segnaposto delle sezioni rese dalla pagina.</summary>
    private async Task<(Document Doc, DocumentVersion Ver)> DocumentoAsync(bool sezioni = true)
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = "vIPI — LIRQ", Language = Language.It, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2611" };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var ver = new DocumentVersion { DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Draft, CreatedByUserId = 0, CreatedUtc = DateTime.UtcNow, AiracCycle = "2611" };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();
        doc.CurrentVersionId = ver.Id;
        if (sezioni)
            foreach (var (chiave, ordine) in new[] { ("runwayrules", 4), ("lvp", 9) })
            {
                var s = new DocumentSection { DocumentVersionId = ver.Id, Title = chiave, Order = ordine, Depth = 0, SectionKey = chiave, RowVersion = Guid.NewGuid().ToByteArray() };
                _db.DocumentSections.Add(s);
                await _db.SaveChangesAsync();
                _db.ContentBlocks.Add(new ContentBlock { DocumentVersionId = ver.Id, SectionId = s.Id, Order = 1, Format = BlockFormat.Table, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, RowVersion = Guid.NewGuid().ToByteArray() });
            }
        await _db.SaveChangesAsync();
        return (doc, ver);
    }

    private async Task<Airport> ScaloAsync(int? vipi, int? vsop, string? notaLvp, params (string? Nome, string? Nota)[] regole)
    {
        var a = new Airport { Icao = "LIRQ", Name = "Firenze", Acc = _acc, DocumentId = vipi, MilDocumentId = vsop };
        _db.Airports.Add(a);
        await _db.SaveChangesAsync();
        _db.AirportLvpMinima.Add(new AirportLvpMinima { AirportId = a.Id, Declared = true, Note = notaLvp });
        for (var i = 0; i < regole.Length; i++)
            _db.AirportRunwayRules.Add(new AirportRunwayRule { AirportId = a.Id, Order = i, Name = regole[i].Nome, DepRunways = "05", ArrRunways = "05", Note = regole[i].Nota });
        await _db.SaveChangesAsync();
        return a;
    }

    private Task<List<string?>> ParagrafiAsync(int versione, string chiave) =>
        _db.ContentBlocks.AsNoTracking()
            .Where(b => b.DocumentVersionId == versione && b.Section!.SectionKey == chiave && b.Format == BlockFormat.Prose)
            .Select(b => b.Body).ToListAsync();

    [Fact]
    public async Task Le_note_diventano_paragrafi_nella_vipi_e_nel_vsop_e_le_colonne_si_svuotano()
    {
        var (vipi, verVipi) = await DocumentoAsync();
        var (vsop, verVsop) = await DocumentoAsync();
        await ScaloAsync(vipi.Id, vsop.Id, "  Avvisare la TWR 10 minuti prima.  ",
            ("Notte", "solo dalle 22"), (null, null), (null, "con vento forte da sud"));

        Assert.True(await _manutenzione.NoteDelloScaloNeiBlocchiAsync() > 0);

        foreach (var ver in new[] { verVipi, verVsop })
        {
            Assert.Equal(new[] { "Avvisare la TWR 10 minuti prima." }, await ParagrafiAsync(ver.Id, "lvp"));
            Assert.Equal(new[] { "- #1 Notte: solo dalle 22\n- #3: con vento forte da sud" }, await ParagrafiAsync(ver.Id, "runwayrules"));
        }
        Assert.Null((await _db.AirportLvpMinima.AsNoTracking().SingleAsync()).Note);
        Assert.All(await _db.AirportRunwayRules.AsNoTracking().ToListAsync(), r => Assert.Null(r.Note));

        Assert.Equal(0, await _manutenzione.NoteDelloScaloNeiBlocchiAsync());
    }

    /// <summary>Senza una sezione dove mettere il testo la nota resta dov'è, e la passata dopo ci riprova.</summary>
    [Fact]
    public async Task Senza_la_sezione_la_nota_resta()
    {
        var (vipi, _) = await DocumentoAsync(sezioni: false);
        await ScaloAsync(vipi.Id, null, "Avvisare la TWR.", ("Notte", "solo dalle 22"));

        Assert.Equal(0, await _manutenzione.NoteDelloScaloNeiBlocchiAsync());

        Assert.Equal("Avvisare la TWR.", (await _db.AirportLvpMinima.AsNoTracking().SingleAsync()).Note);
        Assert.Equal("solo dalle 22", (await _db.AirportRunwayRules.AsNoTracking().SingleAsync()).Note);
    }

    /// <summary>Una nota fatta di soli spazi non porta niente: si svuota e basta.</summary>
    [Fact]
    public async Task Una_nota_di_soli_spazi_si_svuota_senza_paragrafo()
    {
        var (vipi, ver) = await DocumentoAsync();
        await ScaloAsync(vipi.Id, null, "   ", ("Notte", "  "));

        await _manutenzione.NoteDelloScaloNeiBlocchiAsync();

        Assert.Empty(await ParagrafiAsync(ver.Id, "lvp"));
        Assert.Empty(await ParagrafiAsync(ver.Id, "runwayrules"));
        Assert.Null((await _db.AirportLvpMinima.AsNoTracking().SingleAsync()).Note);
        Assert.Null((await _db.AirportRunwayRules.AsNoTracking().SingleAsync()).Note);
    }
}
