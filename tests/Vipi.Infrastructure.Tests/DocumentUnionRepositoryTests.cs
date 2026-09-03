using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le righe di un'unione di documenti (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c>): l'ordine, la
/// guardia che tiene un documento in una sola unione, e la pulizia di ciò che resta quando un membro se ne va.
///
/// <para>⚠️ Il caso che ha deciso il modello sta in archivio, non nella carta: <b>LIBV ha DUE APP non
/// remotizzati</b> (<c>LIBV_APP</c> e <c>LIBV_G_APP</c>), e così LIBN, LIPE, LIRM, LIRS. L'unione è un elenco
/// ordinato, non una coppia — i test qui sotto lavorano su TRE membri apposta.</para>
/// </summary>
public class DocumentUnionRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentUnionRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfDocumentUnionRepository(_db);

        for (var i = 1; i <= 4; i++)
            _db.Documents.Add(new Document
            {
                Id = i, Type = DocumentType.Vipi, Title = $"Documento {i}", Language = Language.It,
                LastUpdatedUtc = DateTime.UtcNow, LastUpdatedAiracCycle = "2609",
            });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Un_unione_nasce_con_due_membri_in_ordine()
    {
        var id = await _repo.CreateAsync(hostDocumentId: 2, guestDocumentId: 1, createdByUserId: 42);

        var righe = await _repo.ByUnionAsync(id);
        Assert.Equal(new[] { 2, 1 }, righe.Select(r => r.DocumentId));
        Assert.Equal(new[] { 0, 1 }, righe.Select(r => r.Order));
        // L'ospite è chi è stato scelto come tale, non chi ha l'id più basso.
        Assert.Equal(2, righe[0].DocumentId);
    }

    [Fact]
    public async Task Il_terzo_membro_va_in_CODA()
    {
        var id = await _repo.CreateAsync(1, 2, 0);
        await _repo.AddMemberAsync(id, 3);

        var righe = await _repo.ByUnionAsync(id);
        Assert.Equal(new[] { 1, 2, 3 }, righe.Select(r => r.DocumentId));
        Assert.Equal(new[] { 0, 1, 2 }, righe.Select(r => r.Order));
    }

    [Fact]
    public async Task Un_documento_sta_in_UNA_SOLA_unione()
    {
        await _repo.CreateAsync(1, 2, 0);

        // L'indice unico su DocumentId è la guardia: senza, due unioni sullo stesso documento nascerebbero in
        // silenzio e si vedrebbero mesi dopo, come una pagina che ripete lo stesso contenuto.
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _repo.CreateAsync(3, 2, 0));
    }

    [Fact]
    public async Task Da_qualunque_membro_si_arriva_alla_stessa_unione()
    {
        var id = await _repo.CreateAsync(1, 2, 0);
        await _repo.AddMemberAsync(id, 3);

        foreach (var docId in new[] { 1, 2, 3 })
        {
            var righe = await _repo.ByDocumentAsync(docId);
            Assert.Equal(id, Assert.Single(righe.Select(r => r.UnionId).Distinct()));
            Assert.Equal(3, righe.Count);
        }

        // Un documento non unito risponde «niente», non «un'unione vuota».
        Assert.Empty(await _repo.ByDocumentAsync(4));
    }

    [Fact]
    public async Task Sposta_scambia_col_vicino_e_ai_bordi_non_fa_niente()
    {
        var id = await _repo.CreateAsync(1, 2, 0);
        await _repo.AddMemberAsync(id, 3);
        var righe = await _repo.ByUnionAsync(id);

        await _repo.MoveAsync(righe[2].MemberId, -1);
        Assert.Equal(new[] { 1, 3, 2 }, (await _repo.ByUnionAsync(id)).Select(r => r.DocumentId));

        // In cima, «su» non fa niente e non protesta: è il tasto che va spento, non l'operazione che deve
        // esplodere — la stessa scelta di MoveSectionAsync.
        var primo = (await _repo.ByUnionAsync(id))[0];
        await _repo.MoveAsync(primo.MemberId, -1);
        Assert.Equal(new[] { 1, 3, 2 }, (await _repo.ByUnionAsync(id)).Select(r => r.DocumentId));
    }

    [Fact]
    public async Task Spostare_l_OSPITE_cambia_l_indirizzo_dell_unione()
    {
        var id = await _repo.CreateAsync(1, 2, 0);
        var righe = await _repo.ByUnionAsync(id);

        await _repo.MoveAsync(righe[0].MemberId, +1);

        // Non è un dettaglio d'ordine: la pagina unita vive all'indirizzo del PRIMO, e chi sposta l'ospite
        // sta spostando la pagina.
        Assert.Equal(2, (await _repo.ByUnionAsync(id))[0].DocumentId);
    }

    [Fact]
    public async Task Togliere_un_membro_RICOMPATTA_le_posizioni()
    {
        var id = await _repo.CreateAsync(1, 2, 0);
        await _repo.AddMemberAsync(id, 3);
        var righe = await _repo.ByUnionAsync(id);

        await _repo.RemoveMemberAsync(righe[1].MemberId);

        var rimasti = await _repo.ByUnionAsync(id);
        Assert.Equal(new[] { 1, 3 }, rimasti.Select(r => r.DocumentId));
        // Un buco nell'ordine non rompe niente, ma rende «sposta giù» un gesto che a volte non muove nulla.
        Assert.Equal(new[] { 0, 1 }, rimasti.Select(r => r.Order));
    }

    [Fact]
    public async Task Sciogliere_non_tocca_i_DOCUMENTI()
    {
        var id = await _repo.CreateAsync(1, 2, 0);

        await _repo.DissolveAsync(id);

        Assert.Empty(await _repo.ListAsync());
        Assert.Equal(0, await _db.DocumentUnions.CountAsync());
        // Sciogliere non è eliminare: i quattro documenti sono tutti ancora lì.
        Assert.Equal(4, await _db.Documents.CountAsync());
    }

    [Fact]
    public async Task Eliminare_un_documento_porta_via_la_sua_appartenenza()
    {
        var id = await _repo.CreateAsync(1, 2, 0);
        await _repo.AddMemberAsync(id, 3);

        _db.Documents.Remove(await _db.Documents.FirstAsync(d => d.Id == 2));
        await _db.SaveChangesAsync();

        // La cascata toglie la riga; l'unione resta viva perché ha ancora due membri.
        Assert.Equal(new[] { 1, 3 }, (await _repo.ByUnionAsync(id)).Select(r => r.DocumentId));
    }

    [Fact]
    public async Task Un_unione_rimasta_con_UN_MEMBRO_si_chiude()
    {
        var id = await _repo.CreateAsync(1, 2, 0);
        _db.Documents.Remove(await _db.Documents.FirstAsync(d => d.Id == 2));
        await _db.SaveChangesAsync();

        var chiuse = await _repo.TidyAsync();

        // Un'unione con un membro solo è una pagina unita che unisce sé stessa, e un redirect che non ha
        // dove mandare.
        Assert.Equal(1, chiuse);
        Assert.Empty(await _repo.ByUnionAsync(id));
        Assert.Equal(0, await _repo.TidyAsync());   // idempotente: gira all'avvio
    }
}
