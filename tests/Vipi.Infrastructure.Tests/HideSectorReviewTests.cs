using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Occultamento settore in /services/vsop/admin/acc: contesto gerarchico per la Regola 1 (blocco radice con figli visibili)
/// e reverse-lookup + flag di revisione per la Regola 3 (segnala i documenti ACC/APP/vLOA impattati).
/// </summary>
public class HideSectorReviewTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(lirr);
        _db.Airports.Add(new Airport { Icao = "LIRP", Name = "Pisa", Acc = lirr });
        // Catalogo: NE radice, TS sotto NE; un aeroporto-APP sotto NE (figlio cross-catalogo).
        _db.AccSectors.AddRange(
            new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR" },
            new AccSector { ComposePosition = "LIRR_TS_CTR", CenterId = "LIRR", Position = "CTR", ParentCallsign = "LIRR_NE_CTR" });
        _db.AirportSectors.Add(
            new AirportSector { ComposePosition = "LIRP_APP", AirportIcao = "LIRP", AccCode = "LIRR", Position = "APP", ParentCallsign = "LIRR_NE_CTR" });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // ---- Regola 1: contesto radice/figli visibili ----

    [Fact]
    public async Task HideContext_Root_With_Visible_Children_Is_Flagged()
    {
        var repo = new EfAccAdminRepository(_db);
        var ne = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_NE_CTR");

        var ctx = await repo.GetSubcenterHideContextAsync(ne.Id);
        Assert.NotNull(ctx);
        Assert.True(ctx!.IsRoot);
        Assert.True(ctx.HasVisibleChildren);   // TS + LIRP_APP visibili
    }

    [Fact]
    public async Task HideContext_NonRoot_Is_Not_Root()
    {
        var repo = new EfAccAdminRepository(_db);
        var ts = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR");

        var ctx = await repo.GetSubcenterHideContextAsync(ts.Id);
        Assert.False(ctx!.IsRoot);   // ha padre NE → l'occultamento è consentito (i figli risalgono)
    }

    [Fact]
    public async Task HideContext_Root_With_All_Children_Hidden_Has_No_Visible_Children()
    {
        // Nascondo i due figli: la radice diventa occultabile.
        foreach (var cs in new[] { "LIRR_TS_CTR" })
            (await _db.AccSectors.FirstAsync(s => s.ComposePosition == cs)).IsHidden = true;
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == "LIRP_APP")).IsHidden = true;
        await _db.SaveChangesAsync();

        var repo = new EfAccAdminRepository(_db);
        var ne = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_NE_CTR");
        var ctx = await repo.GetSubcenterHideContextAsync(ne.Id);
        Assert.False(ctx!.HasVisibleChildren);
    }

    // ---- Regola 3: reverse-lookup + flag revisione ----

    [Fact]
    public async Task Review_FanOut_Flags_Acc_App_And_Vloa_Documents()
    {
        var lirrId = (await _db.Accs.FirstAsync(a => a.Code == "LIRR")).Id;

        var accDoc = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", LastUpdatedAiracCycle = "2607" };
        var appDoc = new Document { Type = DocumentType.Vipi, Title = "APP Pisa", LastUpdatedAiracCycle = "2607" };
        var vloaDoc = new Document { Type = DocumentType.Vloa, Title = "vLOA LIRR ↔ LFMM", LastUpdatedAiracCycle = "2607" };
        var otherAccDoc = new Document { Type = DocumentType.Vipi, Title = "vIPI Milano", LastUpdatedAiracCycle = "2607" };
        _db.Documents.AddRange(accDoc, appDoc, vloaDoc, otherAccDoc);
        await _db.SaveChangesAsync();

        // Sector primario (doc ACC) + Sector APP (doc APP), entrambi in LIRR.
        _db.Sectors.AddRange(
            new Sector { AccId = lirrId, Callsign = "LIRR_NE_CTR", Name = "Roma", Type = SectorType.Ctr,
                Kind = SectorKind.Acc, IsPrimary = true, DocumentId = accDoc.Id, IsProjected = true, IsActive = true },
            new Sector { AccId = lirrId, Callsign = "LIRP_APP", Name = "Pisa APP", Type = SectorType.App,
                Kind = SectorKind.Airport, DocumentId = appDoc.Id, IsProjected = true, IsActive = true });
        // vLOA confinante che cita il settore nascosto.
        _db.NeighbourCandidates.Add(new NeighbourCandidate
        {
            HomeAccCode = "LIRR", ForeignAccCode = "LFMM", ForeignAccName = "Marseille", CountryId = "FR",
            ForeignRootCallsign = "LFMM_CTR", Status = NeighbourCandidateStatus.Confirmed,
            VloaDocumentId = vloaDoc.Id, AdjacentHomeCallsigns = JsonSerializer.Serialize(new[] { "LIRR_TS_CTR" }),
            CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var repo = new EfDocumentReviewRepository(_db);
        var docs = await repo.FindDocumentsForSectorAsync("LIRR_TS_CTR", "LIRR");
        var ids = docs.Select(d => d.Id).ToHashSet();

        Assert.Contains(accDoc.Id, ids);
        Assert.Contains(appDoc.Id, ids);
        Assert.Contains(vloaDoc.Id, ids);
        Assert.DoesNotContain(otherAccDoc.Id, ids);   // altro ACC, nessun settore collegato

        // Set/Get/Clear del flag.
        var now = DateTime.UtcNow;
        await repo.SetReviewAsync(accDoc.Id, now, "motivo");
        var state = await repo.GetReviewAsync(accDoc.Id);
        Assert.Equal("motivo", state!.ReviewReason);
        Assert.NotNull(state.NeedsReviewUtc);

        await repo.ClearReviewAsync(accDoc.Id);
        Assert.Null((await repo.GetReviewAsync(accDoc.Id))!.NeedsReviewUtc);
    }
}
