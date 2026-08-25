using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Release AIRAC (snapshot editoriale per ciclo): verifica snapshot dello stato working, selezione della release
/// effettiva per data, schedulazione futura e sostituzione (Superseded) di release dello stesso ciclo.
/// </summary>
public class ReleaseRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfReleaseRepository _repo = default!;
    private int _docId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = TestReleaseTargets.ReleaseRepo(_db);

        // vLOA minimale: Acc+Sector Home, Document(Vloa) Published con 1 versione, 1 sezione, 1 blocco, party Home.
        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        var home = new Sector { Acc = acc, Callsign = "LIRR_CTR", Type = SectorType.Ctr, Kind = SectorKind.Acc, Name = "Roma", IsActive = true };
        _db.Accs.Add(acc); _db.Sectors.Add(home);

        var doc = new Document { Type = DocumentType.Vloa, Title = "vLOA LIRR ↔ DAAA", Language = Language.En, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Published, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        doc.Parties.Add(new DocumentParty { Document = doc, Sector = home, Role = PartyRole.Home });
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var sec = new DocumentSection { DocumentVersion = ver, Title = "Purpose", Order = 1, Depth = 0, SectionKey = "custom", RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(sec);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersion = ver, Section = sec, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always, Body = "Testo di prova", RowVersion = Guid.NewGuid().ToByteArray() });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();
        _docId = doc.Id;
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Snapshot_Captures_Working_Structure()
    {
        var json = await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, _docId.ToString(), "2606");
        Assert.NotNull(json);
        Assert.Contains("Purpose", json);
        Assert.Contains("Testo di prova", json);
    }

    /// <summary>
    /// Anello fra pubblicazione e pulizia immagini: la pulizia considera «in uso» un'immagine il cui sha compare nel
    /// payload di una release. Se lo snapshot non lo portasse — o lo scrivesse in una forma che lo scanner non sa
    /// leggere — la foto di una vIPI gia' pubblicata risulterebbe orfana e verrebbe cancellata. Qui si verifica
    /// sullo snapshot VERO, non su un payload scritto a mano.
    /// </summary>
    [Fact]
    public async Task Snapshot_Carries_The_Image_Sha_So_Cleanup_Sees_It_As_In_Use()
    {
        const string sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var sec = await _db.DocumentSections.FirstAsync();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = sec.DocumentVersionId, SectionId = sec.Id, Order = 9,
            Format = BlockFormat.Image, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            Body = "Didascalia", BodyJson = Vipi.Application.Content.MediaRef.Serialize(
                new Vipi.Application.Content.MediaRef(sha, "Torre", 800, 600)),
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        var payload = await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, _docId.ToString(), "2606");

        Assert.NotNull(payload);
        Assert.Contains(sha, Vipi.Application.Media.MediaReferenceScanner.Scan(payload));
    }

    [Fact]
    public async Task ScheduledFuture_NotEffectiveNow_ButBecomesEffectiveAtCycle()
    {
        var key = _docId.ToString();
        var json = (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!;
        var future = DateTime.UtcNow.AddDays(28);

        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "9901", future, json, 1, null);

        Assert.Null(await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, DateTime.UtcNow));       // futura: non ancora
        Assert.NotNull(await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, future.AddMinutes(1))); // al ciclo: sì
    }

    /// <summary>
    /// «Una release per ciclo» vale anche per i cicli FUTURI. Prima la marcatura Superseded di
    /// SaveReleaseAsync veniva annullata da RecomputeStatuses (data futura → di nuovo Scheduled): due
    /// ripubblicazioni allo stesso ciclo schedulato lasciavano due «Programmata» gemelle in timeline.
    /// </summary>
    [Fact]
    public async Task Republish_SameFutureCycle_SupersedesTheOlderScheduled()
    {
        var key = _docId.ToString();
        var json = (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!;
        var future = DateTime.UtcNow.AddDays(28);

        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "9901", future, json, 1, "prima stesura");
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "9901", future, json, 1, "correzione");

        var rows = await _db.DocReleases.AsNoTracking()
            .Where(r => r.TargetType == ReleaseTargetType.Vloa && r.TargetKey == key).ToListAsync();
        var scheduled = Assert.Single(rows, r => r.Status == ReleaseStatus.Scheduled);
        Assert.Equal("correzione", scheduled.Note);                       // vince la più recente
        Assert.Single(rows, r => r.Status == ReleaseStatus.Superseded);   // la prima stesura è storia

        // Al ciclo, l'effettiva è la correzione.
        var eff = await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, future.AddMinutes(1));
        Assert.Equal(scheduled.Id, eff!.Id);
    }

    [Fact]
    public async Task Snapshot_AccVipi_Captures_Document_Tree()
    {
        // ACC ora su Document (doc 08e-acc): il settore CTR radice porta un Document vIPI con un blocco Aerovia.
        var home = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_CTR");
        var doc = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Published, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        home.DocumentId = doc.Id; home.IsPrimary = true;
        _db.DocumentSections.Add(new DocumentSection { DocumentVersion = ver, Title = "Settori di aerovia", Order = 1, Depth = 0, SectionKey = "aerovia", RowVersion = Guid.NewGuid().ToByteArray() });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();

        var json = await _repo.SnapshotWorkingAsync(ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", "2606");
        Assert.NotNull(json);
        Assert.Contains("aerovia", json);
        Assert.Contains("Settori di aerovia", json);
    }

    [Fact]
    public async Task Cancel_RemovesRelease_AndPromotesPrevious()
    {
        var key = _docId.ToString();
        var json = (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!;
        var now = DateTime.UtcNow;
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", now.AddDays(-2), json, 1, "vecchia");
        var newId = await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2607", now.AddSeconds(-5), json, 1, "nuova");

        // Ora è effettiva la "nuova" (2607). Annullandola, torna effettiva la "vecchia" (2606).
        var effBefore = await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, DateTime.UtcNow);
        Assert.Equal("nuova", effBefore!.Note);

        var target = await _repo.CancelAsync(newId);
        Assert.Equal((ReleaseTargetType.Vloa, key), target);
        Assert.Null(await _repo.GetByIdAsync(newId));

        var effAfter = await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, DateTime.UtcNow);
        Assert.Equal("vecchia", effAfter!.Note);
    }

    // ---- Caratterizzazione identità per-tipo (rete pre-refactor doc 09 §3a: key→docId via Snapshot, key→accCode) ----

    private async Task<int> SeedVipiDocAsync(string title, string sectionKey)
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = title, Language = Language.It, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Published, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.DocumentSections.Add(new DocumentSection { DocumentVersion = ver, Title = title, Order = 1, Depth = 0, SectionKey = sectionKey, RowVersion = Guid.NewGuid().ToByteArray() });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task AuthAccCode_Vloa_FromHomeParty()
    {
        Assert.Equal("LIRR", await _repo.GetAuthAccCodeAsync(ReleaseTargetType.Vloa, _docId.ToString()));
    }

    [Fact]
    public async Task AuthAccCode_AccVipi_FromKeyPrefix()
    {
        Assert.Equal("LIRR", await _repo.GetAuthAccCodeAsync(ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR"));
    }

    [Fact]
    public async Task AuthAccCode_And_Snapshot_App_Standalone()
    {
        var acc = await _db.Accs.FirstAsync();
        var docId = await SeedVipiDocAsync("vIPI LIRR_APP", "separations");
        _db.Sectors.Add(new Sector { Acc = acc, Callsign = "LIRR_APP", Name = "Roma APP", Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = docId, IsPrimary = true });
        await _db.SaveChangesAsync();

        Assert.Equal("LIRR", await _repo.GetAuthAccCodeAsync(ReleaseTargetType.App, "LIRR_APP"));
        var json = await _repo.SnapshotWorkingAsync(ReleaseTargetType.App, "LIRR_APP", "2606");
        Assert.NotNull(json);
        Assert.Contains("separations", json);
    }

    [Fact]
    public async Task AuthAccCode_And_Snapshot_Airport()
    {
        var acc = await _db.Accs.FirstAsync();
        var docId = await SeedVipiDocAsync("vIPI LIRA", "airportextra");
        _db.Airports.Add(new Airport { Icao = "LIRA", Name = "Ciampino", Acc = acc });
        _db.Sectors.Add(new Sector { Acc = acc, Callsign = "LIRA_TWR", Name = "Ciampino TWR", Type = SectorType.Twr, Kind = SectorKind.Airport, AirportIcao = "LIRA", IsActive = true, DocumentId = docId });
        await _db.SaveChangesAsync();

        Assert.Equal("LIRR", await _repo.GetAuthAccCodeAsync(ReleaseTargetType.Airport, "LIRA"));
        var json = await _repo.SnapshotWorkingAsync(ReleaseTargetType.Airport, "LIRA", "2606");
        Assert.NotNull(json);
        Assert.Contains("airportextra", json);
    }

    [Fact]
    public async Task PublishWorkingVersion_PromotesDraftDocToPublished()
    {
        // Doc APP in BOZZA (mai pubblicato come versione): un settore APP standalone col Document Draft + 1 versione Draft.
        var acc = await _db.Accs.FirstAsync();
        var doc = new Document { Type = DocumentType.Vipi, Title = "vIPI LIPZ_APP", Language = Language.It, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.Sectors.Add(new Sector { Acc = acc, Callsign = "LIPZ_APP", Name = "Pisa APP", Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = doc.Id, IsPrimary = true });
        await _db.SaveChangesAsync();

        await _repo.PublishWorkingVersionAsync(ReleaseTargetType.App, "LIPZ_APP", 1, "2607");

        var reloaded = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == doc.Id);
        Assert.Equal(DocumentStatus.Published, reloaded.Status);
        Assert.Equal(ver.Id, reloaded.CurrentVersionId);
        Assert.Equal(DocumentStatus.Published, (await _db.DocumentVersions.AsNoTracking().FirstAsync(v => v.Id == ver.Id)).Status);
    }

    [Fact]
    public async Task PublishWorkingVersion_NoDraft_IsNoOp()
    {
        // Il doc vLOA seed è già Published con la sua versione; nessuna bozza → no-op, nessuna eccezione.
        await _repo.PublishWorkingVersionAsync(ReleaseTargetType.Vloa, _docId.ToString(), 1, "2607");
        var reloaded = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == _docId);
        Assert.Equal(DocumentStatus.Published, reloaded.Status);
    }

    [Fact]
    public async Task PublishNow_IsImmediatelyEffective_AndSupersedesSameCycle()
    {
        var key = _docId.ToString();
        var json = (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!;
        var now = DateTime.UtcNow;

        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", now.AddSeconds(-10), json, 1, "prima");
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", now, json, 1, "review");

        var eff = await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, DateTime.UtcNow);
        Assert.NotNull(eff);
        Assert.Equal("review", eff!.Note);   // l'ultima dello stesso ciclo vince

        var list = await _repo.ListAsync(ReleaseTargetType.Vloa, key);
        Assert.Single(list, r => r.IsEffectiveNow);                       // una sola in vigore
        Assert.Contains(list, r => r.Status == ReleaseStatus.Superseded); // la prima superata
    }

    [Fact]
    public async Task PruneReleases_RemovesOnlyOldSuperseded_KeepsEffectiveScheduledAndRecent_AndIsIdempotent()
    {
        var key = _docId.ToString();
        var json = (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!;
        var now = DateTime.UtcNow;

        // Tre cicli diversi: due passati (il più recente diventa Effective, l'altro Superseded) + una futura Scheduled.
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2401", now.AddDays(-400), json, 1, "vecchissima"); // Superseded, oltre soglia
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2605", now.AddDays(-60), json, 1, "recente");      // Superseded, entro soglia
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", now.AddDays(-1), json, 1, "attuale");       // Effective
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2699", now.AddDays(28), json, 1, "futura");        // Scheduled

        var cutoff = now.AddDays(-100);
        var removed = await _repo.PruneReleasesAsync(ReleaseTargetType.Vloa, key, cutoff);
        Assert.Equal(1, removed);   // solo la "vecchissima" (Superseded, eff < cutoff)

        var list = await _repo.ListAsync(ReleaseTargetType.Vloa, key);
        Assert.DoesNotContain(list, r => r.Note == "vecchissima");
        Assert.Contains(list, r => r.Note == "recente");    // Superseded ma entro soglia → tenuta
        Assert.Contains(list, r => r.Note == "attuale" && r.IsEffectiveNow);
        Assert.Contains(list, r => r.Note == "futura" && r.Status == ReleaseStatus.Scheduled);

        // Idempotente: seconda passata non rimuove nulla.
        Assert.Equal(0, await _repo.PruneReleasesAsync(ReleaseTargetType.Vloa, key, cutoff));
    }

    /// <summary>
    /// Gli stati in DB invecchiano da soli: una schedulata entra in vigore col passare del tempo e la vecchia
    /// riga resta marcata Effective finché qualcuno non risalva. Lo sweep di boot (PruneAllAsync) non passa da
    /// SaveReleaseAsync: prima potava solo ciò che un salvataggio passato aveva già marcato Superseded, e la
    /// riga superata-per-tempo gli sfuggiva a ogni giro. Ora la potatura ricalcola prima di guardare.
    /// </summary>
    [Fact]
    public async Task Prune_Recomputes_Stale_Statuses_Before_Pruning()
    {
        var key = _docId.ToString();
        var now = DateTime.UtcNow;

        // La storia com'era all'ULTIMO salvataggio: A in vigore, B schedulata. Poi il tempo è passato —
        // B è entrata in vigore da sola — e nessun salvataggio ha ricalcolato gli stati.
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.Vloa, TargetKey = key, VersionNumber = 1, ReleaseAiracCycle = "2501",
            ReleaseEffectiveUtc = now.AddDays(-500), Status = ReleaseStatus.Effective, PayloadJson = "{}",
            CreatedByUserId = 1, CreatedUtc = now.AddDays(-500),
        });
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.Vloa, TargetKey = key, VersionNumber = 2, ReleaseAiracCycle = "2502",
            ReleaseEffectiveUtc = now.AddDays(-1), Status = ReleaseStatus.Scheduled, PayloadJson = "{}",
            CreatedByUserId = 1, CreatedUtc = now.AddDays(-30),
        });
        await _db.SaveChangesAsync();

        var removed = await _repo.PruneReleasesAsync(ReleaseTargetType.Vloa, key, keepFromUtc: now.AddDays(-365));

        Assert.Equal(1, removed);   // A: superata per tempo e oltre soglia — con gli stati stantii sfuggiva
        var rest = Assert.Single(await _db.DocReleases.AsNoTracking()
            .Where(r => r.TargetType == ReleaseTargetType.Vloa && r.TargetKey == key).ToListAsync());
        Assert.Equal(2, rest.VersionNumber);
        Assert.Equal(ReleaseStatus.Effective, rest.Status);   // e lo stato di B è riallineato al fatto
    }

    /// <summary>
    /// L'anello mancante della pulizia immagini: la potatura e l'annullo di una release non passavano da
    /// <c>DeleteOrphansAsync</c>, quindi una foto citata SOLO da release rimosse restava nel deposito per
    /// sempre (la scoprivi soltanto dall'analisi manuale in admin). La liberazione resta prudente: decide
    /// <c>DeleteOrphansAsync</c>, che ricontrolla tutte le sorgenti — qui il blocco bozza cita ancora
    /// la SECONDA foto, che infatti sopravvive.
    /// </summary>
    [Fact]
    public async Task Prune_And_Cancel_Free_Images_Cited_Only_By_Removed_Releases()
    {
        const string shaSoloRelease = "1111111111111111111111111111111111111111111111111111111111111111";
        const string shaAncheInBozza = "2222222222222222222222222222222222222222222222222222222222222222";
        _db.MediaAssets.Add(new MediaAsset { Sha256 = shaSoloRelease, ContentType = "image/png", ByteSize = 1, Bytes = new byte[] { 1 }, CreatedUtc = DateTime.UtcNow });
        _db.MediaAssets.Add(new MediaAsset { Sha256 = shaAncheInBozza, ContentType = "image/png", ByteSize = 1, Bytes = new byte[] { 2 }, CreatedUtc = DateTime.UtcNow });
        var sec = await _db.DocumentSections.FirstAsync();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = sec.DocumentVersionId, SectionId = sec.Id, Order = 8,
            Format = BlockFormat.Image, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            BodyJson = Vipi.Application.Content.MediaRef.Serialize(new Vipi.Application.Content.MediaRef(shaAncheInBozza, "Ancora usata", 10, 10)),
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        var key = _docId.ToString();
        var now = DateTime.UtcNow;
        var payload = $"{{\"foto\":[\"{shaSoloRelease}\",\"{shaAncheInBozza}\"]}}";

        // Due release vecchie col payload che cita le foto + una attuale pulita: la potatura toglie le vecchie.
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2401", now.AddDays(-400), payload, 1, "vecchia");
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", now.AddDays(-1),
            (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!, 1, "attuale");
        Assert.Equal(1, await _repo.PruneReleasesAsync(ReleaseTargetType.Vloa, key, now.AddDays(-100)));

        Assert.Null(await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Sha256 == shaSoloRelease));   // liberata
        Assert.NotNull(await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Sha256 == shaAncheInBozza)); // ancora citata dal blocco

        // Annullo dell'attuale: il suo payload (lo snapshot vero) cita shaAncheInBozza? No — quindi qui si
        // verifica solo che l'annullo passi dallo stesso anello senza rompere nulla di citato altrove.
        var relId = await _db.DocReleases.Where(r => r.TargetKey == key).Select(r => r.Id).FirstAsync();
        await _repo.CancelAsync(relId);
        Assert.NotNull(await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Sha256 == shaAncheInBozza));
    }

    /// <summary>
    /// Eliminare un documento cancella versioni e blocchi via cascade EF — senza passare da
    /// EliminaVersioneAsync, quindi senza la sua scansione sha — e porta via anche le release del bersaglio:
    /// prima nessuno liberava le foto citate solo lì. Ora DeleteAsync raccoglie gli sha PRIMA (blocchi di
    /// tutte le versioni + payload delle release) e in coda lascia decidere a DeleteOrphansAsync.
    /// </summary>
    [Fact]
    public async Task DeleteDocument_Frees_Images_Cited_Only_By_It()
    {
        const string sha = "3333333333333333333333333333333333333333333333333333333333333333";
        _db.MediaAssets.Add(new MediaAsset { Sha256 = sha, ContentType = "image/png", ByteSize = 1, Bytes = new byte[] { 3 }, CreatedUtc = DateTime.UtcNow });
        var sec = await _db.DocumentSections.FirstAsync();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = sec.DocumentVersionId, SectionId = sec.Id, Order = 7,
            Format = BlockFormat.Image, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            BodyJson = Vipi.Application.Content.MediaRef.Serialize(new Vipi.Application.Content.MediaRef(sha, "Solo qui", 10, 10)),
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        var key = _docId.ToString();
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", DateTime.UtcNow.AddDays(-1),
            (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!, 1, null);

        var admin = TestReleaseTargets.AdminRepo(_db);
        await admin.DeleteAsync(new Vipi.Application.Content.ManagedDocRef(Vipi.Application.Content.ManagedDocKind.Vloa, key, _docId), actorUserId: 1);

        Assert.Empty(await _db.DocReleases.AsNoTracking().Where(r => r.TargetKey == key).ToListAsync());
        Assert.Null(await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == _docId));
        Assert.Null(await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Sha256 == sha));   // liberata
    }
}
