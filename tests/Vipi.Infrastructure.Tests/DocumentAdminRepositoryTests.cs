using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Caratterizzazione di <see cref="EfDocumentAdminRepository"/> (rete pre-refactor doc 09 §3a): l'elenco unificato
/// deve derivare per ciascun tipo di Document il giusto <see cref="ManagedDocKind"/>, la chiave di release e l'ACC.
/// Post doc 08 tutti e 4 i tipi sono su Document; questo fissa la mappatura shape→identità prima di spostarla nei descrittori.
/// </summary>
public class DocumentAdminRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentAdminRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = TestReleaseTargets.AdminRepo(_db);

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        var lfff = new Acc { Code = "LFFF", Name = "France", CountryPrefix = "LF" };
        _db.Accs.AddRange(lirr, lfff);
        await _db.SaveChangesAsync();

        // vLOA: Document(Vloa) con party Home(LIRR) + Neighbour(LFFF).
        var vloa = new Document { Type = DocumentType.Vloa, Title = "vLOA LIRR ↔ LFFF", Language = Language.En, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var homeSec = new Sector { Acc = lirr, Callsign = "LIRR_CTR", Name = "Roma", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true };
        var foreignSec = new Sector { Acc = lfff, Callsign = "LFFF_CTR", Name = "France", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true };
        vloa.Parties.Add(new DocumentParty { Document = vloa, Sector = homeSec, Role = PartyRole.Home });
        vloa.Parties.Add(new DocumentParty { Document = vloa, Sector = foreignSec, Role = PartyRole.Neighbour });
        _db.Sectors.AddRange(homeSec, foreignSec);
        _db.Documents.Add(vloa);

        // ACC: Document(Vipi) col CTR radice primario dell'ACC.
        var accDoc = await NewVipiDocAsync("vIPI Roma");
        _db.Sectors.Add(new Sector { Acc = lirr, Callsign = "LIRR_ROOT_CTR", Name = "Roma root", Type = SectorType.Ctr, Kind = SectorKind.Acc, ParentSectorId = null, IsActive = true, DocumentId = accDoc, IsPrimary = true });

        // APP standalone: Document(Vipi) col settore APP primario.
        var appDoc = await NewVipiDocAsync("vIPI LIRR_APP");
        _db.Sectors.Add(new Sector { Acc = lirr, Callsign = "LIRR_APP", Name = "Roma APP", Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = appDoc, IsPrimary = true });

        // Aeroporto: Document(Vipi) col settore aeroporto (ICAO).
        var airDoc = await NewVipiDocAsync("vIPI LIRA");
        _db.Airports.Add(new Airport { Icao = "LIRA", Name = "Ciampino", Acc = lirr });
        _db.Sectors.Add(new Sector { Acc = lirr, Callsign = "LIRA_TWR", Name = "Ciampino TWR", Type = SectorType.Twr, Kind = SectorKind.Airport, AirportIcao = "LIRA", IsActive = true, DocumentId = airDoc, IsPrimary = true });

        await _db.SaveChangesAsync();
    }

    private async Task<int> NewVipiDocAsync(string title)
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = title, Language = Language.It, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Published, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task List_DerivesKindKeyAndAcc_PerType()
    {
        var all = await _repo.ListAsync();

        var vloa = Assert.Single(all, m => m.Kind == ManagedDocKind.Vloa);
        Assert.Equal(ReleaseTargetType.Vloa, vloa.ReleaseTarget);
        Assert.Equal("LIRR", vloa.AccCode);
        Assert.Equal("LFFF", vloa.NeighbourCode);

        var acc = Assert.Single(all, m => m.Kind == ManagedDocKind.AccVipi);
        Assert.Equal("LIRR|LIRR_ROOT_CTR", acc.ReleaseKey);
        Assert.Equal("LIRR", acc.AccCode);

        var app = Assert.Single(all, m => m.Kind == ManagedDocKind.AppVipi);
        Assert.Equal("LIRR_APP", app.ReleaseKey);
        Assert.Equal("LIRR", app.AccCode);

        var air = Assert.Single(all, m => m.Kind == ManagedDocKind.AirportVipi);
        Assert.Equal("LIRA", air.ReleaseKey);
        Assert.Equal("LIRR", air.AccCode);
    }

    [Theory]
    [InlineData(ManagedDocKind.AccVipi, "LIRR|LIRR_ROOT_CTR")]
    [InlineData(ManagedDocKind.AppVipi, "LIRR_APP")]
    [InlineData(ManagedDocKind.AirportVipi, "LIRA")]
    public async Task GetAccCode_ResolvesLirr_PerType(ManagedDocKind kind, string key)
    {
        Assert.Equal("LIRR", await _repo.GetAccCodeAsync(new ManagedDocRef(kind, key, null)));
    }

    [Fact]
    public async Task GetAccCode_Vloa_FromDocumentHomeParty()
    {
        var vloa = Assert.Single(await _repo.ListAsync(), m => m.Kind == ManagedDocKind.Vloa);
        Assert.Equal("LIRR", await _repo.GetAccCodeAsync(new ManagedDocRef(ManagedDocKind.Vloa, vloa.ReleaseKey, vloa.DocumentId)));
    }

    [Fact]
    public async Task List_HasEffectiveRelease_TracksActiveReleaseNotJustPublishedStatus()
    {
        // Tutti i doc sono Published ma SENZA release → gate visibilità pubblica (doc 10 §3f) false per tutti.
        Assert.All(await _repo.ListAsync(), m => Assert.False(m.HasEffectiveRelease));

        // Release AIRAC in vigore ORA per il solo aeroporto (chiave = ICAO) → solo lui diventa pubblicamente visibile.
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.Airport, TargetKey = "LIRA", VersionNumber = 1,
            ReleaseAiracCycle = "2606", ReleaseEffectiveUtc = DateTime.UtcNow.AddMinutes(-1),
            Status = ReleaseStatus.Effective, PayloadJson = "{}", CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var all = await _repo.ListAsync();
        Assert.True(Assert.Single(all, m => m.Kind == ManagedDocKind.AirportVipi).HasEffectiveRelease);
        Assert.False(Assert.Single(all, m => m.Kind == ManagedDocKind.AppVipi).HasEffectiveRelease);   // published ma senza release
    }
}
