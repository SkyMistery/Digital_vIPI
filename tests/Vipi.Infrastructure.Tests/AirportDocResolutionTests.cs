using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.ReleaseTargets;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// ICAO → documento d'AEROPORTO quando sullo stesso aeroporto vive anche un APP non remotizzato.
/// L'APP standalone (LIRP_APP) è un settore Kind=Airport con AirportIcao=LIRP, ma ha un documento suo:
/// senza escluderlo, chi cerca «il documento di LIRP» pescava l'APP — e la pagina pubblica dell'aeroporto
/// mostrava (e la release fotografava) il documento APP.
/// </summary>
public class AirportDocResolutionTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private int _appDocId;
    private int _airportDocId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        // Ordine voluto: l'APP nasce PRIMA, così senza il filtro è lui il primo che il database restituisce.
        _appDocId = await AttachDocAsync("LIRP_APP", "Pisa Approach");
        _airportDocId = await AttachDocAsync("LIRP_TWR", "vIPI — LIRP Pisa");
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Crea un documento pubblicato con una sezione e lo lega al settore indicato come primario.</summary>
    private async Task<int> AttachDocAsync(string callsign, string title)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = title, Language = Language.It,
            Status = DocumentStatus.Published, LastUpdatedUtc = DateTime.UtcNow, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var ver = new DocumentVersion
        {
            DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Published,
            CreatedUtc = DateTime.UtcNow, AiracCycle = "2608",
        };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();

        _db.DocumentSections.Add(new DocumentSection
        {
            DocumentVersionId = ver.Id, Title = title, Order = 0, Depth = 0, SectionKey = "validity",
        });
        doc.CurrentVersionId = ver.Id;

        var sec = await _db.Sectors.FirstAsync(s => s.Callsign == callsign);
        sec.DocumentId = doc.Id;
        sec.IsPrimary = true;
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task ReleaseTarget_Aeroporto_Non_Risolve_Il_Documento_Dell_App_Standalone()
    {
        var id = await new AirportReleaseTarget(_db).ResolveDocumentIdAsync("LIRP");
        Assert.Equal(_airportDocId, id);
        Assert.NotEqual(_appDocId, id);
    }

    [Fact]
    public async Task GetDocumentId_Aeroporto_Non_Risolve_Il_Documento_Dell_App_Standalone()
    {
        var repo = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        Assert.Equal(_airportDocId, await repo.GetDocumentIdAsync("LIRP"));
    }

    [Fact]
    public async Task Vista_Pubblica_Aeroporto_Carica_Il_Documento_Aeroporto_Non_L_App()
    {
        var registry = new ReleaseTargetRegistry(new IReleaseTarget[]
        {
            new AppReleaseTarget(_db), new AccVipiReleaseTarget(_db), new AirportReleaseTarget(_db), new VloaReleaseTarget(_db),
        });
        var content = new EfContentRepository(_db, new EfReleaseRepository(_db, registry, new EfMediaMaintenance(_db)));

        // preferWorking: salta la release e legge lo stato di lavorazione — quel che conta qui è QUALE documento.
        var raw = await content.LoadAirportVipiAsync("LIRP", ignoreRelease: true, preferWorking: true);

        Assert.NotNull(raw);
        Assert.Equal("vIPI — LIRP Pisa", raw!.Title);
    }
}
