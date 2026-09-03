using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Riparentazione di una sezione: <c>MoveSectionToParentAsync</c>, carta 2026-09-04.
///
/// <para>È l'unica mossa che cambia il gruppo di una sezione — il riordino, frecce e trascinamento, sposta
/// solo fra fratelli apposta. Qui si provano le sue <b>cinque guardie</b> (bozza, sezione libera, stessa
/// versione, niente cicli, profondità del sottoalbero) e le due cose che si dimenticano: la colonna
/// <c>Depth</c> riscritta su tutto il sottoalbero, e i <b>due</b> gruppi rinumerati — quello che riceve e
/// quello che perde.</para>
/// </summary>
public class SezioniRiparentateTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);
        _repo = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<int> BozzaAsync()
    {
        var docId = await _db.Documents.Where(d => d.Type == DocumentType.Vipi).Select(d => d.Id).FirstAsync();
        return await _repo.CreateDraftAsync(docId, authorUserId: 1);
    }

    private Task<int> SezioneAsync(int draftId, int? padre, string titolo) =>
        _repo.AddSectionAsync(draftId, padre, titolo, BlockSection.Other);

    /// <summary>Il gesto intero: la sezione cambia padre, il gruppo che la riceve la ospita nel posto chiesto
    /// e quello che la perde si richiude.</summary>
    [Fact]
    public async Task Sposta_una_sezione_libera_e_rinumera_i_due_gruppi()
    {
        var draft = await BozzaAsync();
        var a = await SezioneAsync(draft, null, "A");
        var b = await SezioneAsync(draft, null, "B");
        var a1 = await SezioneAsync(draft, a, "A1");
        var a2 = await SezioneAsync(draft, a, "A2");
        var b1 = await SezioneAsync(draft, b, "B1");

        // A2 va sotto B, PRIMA di B1.
        await _repo.MoveSectionToParentAsync(a2, b, b1);

        Assert.Equal(b, await PadreAsync(a2));
        Assert.Equal(new[] { a2, b1 }, await FigliOrdinateAsync(b));
        // Il gruppo che l'ha persa resta con la sola A1, e riparte da uno: Order è una posizione.
        Assert.Equal(new[] { a1 }, await FigliOrdinateAsync(a));
        Assert.Equal(1, await _db.DocumentSections.AsNoTracking().Where(s => s.Id == a1).Select(s => s.Order).FirstAsync());
    }

    /// <summary>Riferimento nullo = in coda al gruppo di destinazione.</summary>
    [Fact]
    public async Task Senza_riferimento_accoda()
    {
        var draft = await BozzaAsync();
        var a = await SezioneAsync(draft, null, "A");
        var b = await SezioneAsync(draft, null, "B");
        var b1 = await SezioneAsync(draft, b, "B1");
        var a1 = await SezioneAsync(draft, a, "A1");

        await _repo.MoveSectionToParentAsync(a1, b, null);

        Assert.Equal(new[] { b1, a1 }, await FigliOrdinateAsync(b));
    }

    /// <summary>Si torna anche alla RADICE del documento: il padre nullo è una destinazione, non un'assenza.</summary>
    [Fact]
    public async Task Riporta_alla_radice()
    {
        var draft = await BozzaAsync();
        var a = await SezioneAsync(draft, null, "A");
        var a1 = await SezioneAsync(draft, a, "A1");

        await _repo.MoveSectionToParentAsync(a1, null, null);

        Assert.Null(await PadreAsync(a1));
        Assert.Equal(0, await ProfonditaAsync(a1));
    }

    /// <summary>⚠️ `Depth` è una COLONNA: la riscrive tutto il sottoalbero, non la sola sezione mossa. Chi la
    /// lascia indietro ottiene figlie che si rendono al livello sbagliato.</summary>
    [Fact]
    public async Task Riscrive_la_profondita_di_tutto_il_sottoalbero()
    {
        var draft = await BozzaAsync();
        var a = await SezioneAsync(draft, null, "A");
        var b = await SezioneAsync(draft, null, "B");
        var a1 = await SezioneAsync(draft, a, "A1");
        var a1a = await SezioneAsync(draft, a1, "A1a");

        await _repo.MoveSectionToParentAsync(a1, b, null);

        Assert.Equal(1, await ProfonditaAsync(a1));
        Assert.Equal(2, await ProfonditaAsync(a1a));
    }

    /// <summary>Guardia: una sezione di CATALOGO ha un posto standard e non cambia gruppo.</summary>
    [Fact]
    public async Task Rifiuta_una_sezione_di_catalogo()
    {
        var draft = await BozzaAsync();
        var libera = await SezioneAsync(draft, null, "Libera");
        var diCatalogo = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == draft && !s.SectionKey.StartsWith("custom"))
            .OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.MoveSectionToParentAsync(diCatalogo, libera, null));
    }

    /// <summary>Guardia: il ciclo. Un padre dentro il proprio sottoalbero sparirebbe dall'albero e non
    /// tornerebbe — nessun ciclo esterno lo raggiungerebbe più.</summary>
    [Fact]
    public async Task Rifiuta_il_ciclo()
    {
        var draft = await BozzaAsync();
        var a = await SezioneAsync(draft, null, "A");
        var a1 = await SezioneAsync(draft, a, "A1");
        var a1a = await SezioneAsync(draft, a1, "A1a");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.MoveSectionToParentAsync(a, a1, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.MoveSectionToParentAsync(a, a1a, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.MoveSectionToParentAsync(a, a, null));
        Assert.Null(await PadreAsync(a));
    }

    /// <summary>Guardia: la profondità si misura sul SOTTOALBERO. A ha una figlia con una figlia, quindi ne
    /// porta due: sotto una sezione già profonda uno non ci sta.</summary>
    [Fact]
    public async Task Rifiuta_se_il_sottoalbero_sfora_la_profondita()
    {
        var draft = await BozzaAsync();
        var a = await SezioneAsync(draft, null, "A");
        var a1 = await SezioneAsync(draft, a, "A1");
        await SezioneAsync(draft, a1, "A1a");

        var b = await SezioneAsync(draft, null, "B");
        var b1 = await SezioneAsync(draft, b, "B1");
        var b1a = await SezioneAsync(draft, b1, "B1a");

        // A (altezza 2) sotto B1 (profondità 1) darebbe un livello 4: rifiutata.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.MoveSectionToParentAsync(a, b1, null));
        // La stessa A sotto B (profondità 0) ci sta esatta: 1 + 2 = 3, il massimo.
        await _repo.MoveSectionToParentAsync(a, b, null);
        Assert.Equal(b, await PadreAsync(a));
        // ...e la figlia di B1, che non c'entra, non l'ha toccata nessuno.
        Assert.Equal(2, await ProfonditaAsync(b1a));
    }

    /// <summary>Guardia: un riferimento che non è del gruppo di destinazione vuol dire albero vecchio in mano
    /// a chi ha chiesto la mossa. Si rifiuta: accodare in silenzio metterebbe la sezione dove nessuno ha
    /// chiesto, ed è peggio che non muoverla.</summary>
    [Fact]
    public async Task Rifiuta_un_riferimento_di_un_altro_gruppo()
    {
        var draft = await BozzaAsync();
        var a = await SezioneAsync(draft, null, "A");
        var b = await SezioneAsync(draft, null, "B");
        var a1 = await SezioneAsync(draft, a, "A1");
        var a2 = await SezioneAsync(draft, a, "A2");

        // A2 sotto B, ma «prima di A1» — che sotto B non c'è.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.MoveSectionToParentAsync(a2, b, a1));
        Assert.Equal(a, await PadreAsync(a2));
    }

    /// <summary>Guardia: una versione PUBBLICATA non si tocca, come per ogni altra mutazione di sezione.</summary>
    [Fact]
    public async Task Rifiuta_una_versione_pubblicata()
    {
        var docId = await _db.Documents.Where(d => d.Type == DocumentType.Vipi).Select(d => d.Id).FirstAsync();
        var pubblicata = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();
        var due = await _db.DocumentSections.Where(s => s.DocumentVersionId == pubblicata)
            .OrderBy(s => s.Id).Select(s => s.Id).Take(2).ToArrayAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.MoveSectionToParentAsync(due[0], due[1], null));
    }

    /// <summary>Un padre di un'ALTRA versione non è una destinazione: una sezione non cambia documento.</summary>
    [Fact]
    public async Task Rifiuta_un_padre_di_un_altra_versione()
    {
        var draft = await BozzaAsync();
        var a = await SezioneAsync(draft, null, "A");
        var altrove = await _db.DocumentSections
            .Where(s => s.DocumentVersionId != draft).OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.MoveSectionToParentAsync(a, altrove, null));
    }

    private async Task<int?> PadreAsync(int sectionId) => await _db.DocumentSections.AsNoTracking()
        .Where(s => s.Id == sectionId).Select(s => s.ParentSectionId).FirstAsync();

    private async Task<int> ProfonditaAsync(int sectionId) => await _db.DocumentSections.AsNoTracking()
        .Where(s => s.Id == sectionId).Select(s => s.Depth).FirstAsync();

    private async Task<int[]> FigliOrdinateAsync(int parentId) => await _db.DocumentSections.AsNoTracking()
        .Where(s => s.ParentSectionId == parentId)
        .OrderBy(s => s.Order).ThenBy(s => s.Id).Select(s => s.Id).ToArrayAsync();
}
