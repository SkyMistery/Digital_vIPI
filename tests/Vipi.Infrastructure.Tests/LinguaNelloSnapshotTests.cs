using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La lingua di un documento arriva a chi legge e a chi pubblica (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c> §2).
///
/// <para>
/// ⚠️ <b>Questo file esiste per un difetto che nessun test poteva vedere.</b> Fino al 31 agosto 2026
/// <c>BuildRawFromVersionAsync</c> non copiava <c>Document.Language</c> nello snapshot di release: sul
/// <c>vipi.db</c> vero <b>13 payload su 13</b> dicevano <c>"Language":null</c>. Da lì, in silenzio, il
/// congelamento delle traduzioni non è MAI scattato (<c>ReleaseService</c> esce subito quando la lingua è
/// nulla) e la prosa derivata si è sempre congelata in italiano, anche per una vLOA che nasce inglese.
/// </para>
///
/// <para>
/// ⚠️ I test che c'erano coprivano il lato del <b>lettore</b> — costruendo le viste a mano, con la lingua
/// già dentro — e mai quello di chi <b>scatta la fotografia</b>. Un modello di prova che si costruisce da
/// sé non può accorgersi di un campo che la produzione non riempie.
/// </para>
/// </summary>
public class LinguaNelloSnapshotTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task<(Document Doc, int VersionId)> DocumentoAsync(Language lingua, bool bloccato)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vSOP — LIPA", Language = lingua, LanguageLocked = bloccato,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2609",
        };
        _db.Documents.Add(doc);
        var version = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Published,
            CreatedByUserId = 1, CreatedUtc = DateTime.UtcNow, AiracCycle = "2609",
        };
        _db.DocumentVersions.Add(version);
        _db.DocumentSections.Add(new DocumentSection
        {
            DocumentVersion = version, Title = "General data", Order = 1, Depth = 0,
            SectionKey = "generaldata", RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();
        return (doc, version.Id);
    }

    [Fact]
    public async Task Lo_snapshot_porta_la_lingua_del_documento()
    {
        var (doc, versionId) = await DocumentoAsync(Language.En, bloccato: false);

        var raw = await EfContentRepository.BuildRawFromVersionAsync(_db, versionId, doc.Title, "2609", default, doc);

        Assert.NotNull(raw);
        Assert.Equal(Language.En, raw!.Language);
        Assert.False(raw.LanguageLocked);
    }

    [Fact]
    public async Task Lo_snapshot_porta_anche_il_blocco()
    {
        var (doc, versionId) = await DocumentoAsync(Language.En, bloccato: true);

        var raw = await EfContentRepository.BuildRawFromVersionAsync(_db, versionId, doc.Title, "2609", default, doc);

        Assert.True(raw!.LanguageLocked);
    }

    [Fact]
    public async Task Senza_documento_la_lingua_resta_ignota_e_non_diventa_italiano()
    {
        // ⚠️ Il ripiego è «non si sa», non «italiano»: una vLOA nasce inglese, e darle l'italiano per
        // default vorrebbe dire tradurre testo inglese come se fosse italiano — la memoria mancherebbe su
        // ogni frase, e chi legge non capirebbe perché.
        var (doc, versionId) = await DocumentoAsync(Language.En, bloccato: true);

        var raw = await EfContentRepository.BuildRawFromVersionAsync(_db, versionId, doc.Title, "2609", default);

        Assert.Null(raw!.Language);
        Assert.False(raw.LanguageLocked);
    }
}
