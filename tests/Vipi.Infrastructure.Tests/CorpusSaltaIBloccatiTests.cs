using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il corpus da tradurre non contiene i documenti a <b>lingua bloccata</b> (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c> §6).
///
/// <para>
/// ⚠️ <b>È la parte che si paga in denaro.</b> Il giro di riempimento manda al motore tutto ciò che il
/// corpus dice: una frase di un documento bloccato sarebbe un pagamento per una risposta che nessuno vedrà
/// mai — e una riga in più nel Registro, che chi rivede non saprebbe dove va a finire.
/// </para>
/// </summary>
public class CorpusSaltaIBloccatiTests : IAsyncLifetime
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

    private async Task ScriviAsync(string titoloSezione, string prosa, bool bloccato)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI", Language = Language.It, LanguageLocked = bloccato,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2609",
        };
        _db.Documents.Add(doc);
        var version = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Published,
            CreatedByUserId = 1, CreatedUtc = DateTime.UtcNow, AiracCycle = "2609",
        };
        _db.DocumentVersions.Add(version);
        var sezione = new DocumentSection
        {
            DocumentVersion = version, Title = titoloSezione, Order = 1, Depth = 0,
            SectionKey = "custom:abc", RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(sezione);
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersion = version, Section = sezione, Order = 1,
            Format = BlockFormat.Prose, Tier = BlockTier.Extended, Body = prosa,
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task La_prosa_di_un_documento_bloccato_non_entra_nel_corpus()
    {
        await ScriviAsync("Separazioni", "Contatta la torre.", bloccato: false);
        await ScriviAsync("Nominativi", "Chiama Aviano Ground.", bloccato: true);

        var segmenti = await new EfTranslatableCorpus(_db).SegmentiAsync("it");

        Assert.Contains("Contatta la torre.", segmenti);
        Assert.Contains("Separazioni", segmenti);

        // ⚠️ Anche il TITOLO della sezione, non solo il corpo: i titoli sono la metà del corpus, e la loro
        // query è un'altra — escluderne una sola lascerebbe metà del documento a pagamento.
        Assert.DoesNotContain("Chiama Aviano Ground.", segmenti);
        Assert.DoesNotContain("Nominativi", segmenti);
    }

    [Fact]
    public async Task Una_frase_presente_ANCHE_altrove_resta_nel_corpus()
    {
        // ⚠️ Qui non si nasconde un testo, si smette di chiederlo: la memoria è indicizzata sulla FRASE,
        // non sul documento. La stessa frase in un documento non bloccato si traduce come sempre — e chi
        // leggesse questo test al contrario penserebbe che il blocco «censuri» delle frasi.
        await ScriviAsync("Separazioni", "Contatta la torre.", bloccato: true);
        await ScriviAsync("Separazioni", "Contatta la torre.", bloccato: false);

        var segmenti = await new EfTranslatableCorpus(_db).SegmentiAsync("it");

        Assert.Contains("Contatta la torre.", segmenti);
    }
}
