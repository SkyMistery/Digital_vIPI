using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// A parità di <c>Order</c>: chi legge e chi sposta devono vedere lo stesso ordine, e la freccia deve
/// muovere davvero (carta 2026-09-04, S6).
///
/// <para>⚠️ <c>Order</c> è una POSIZIONE fra fratelli, non un identificativo: nessun indice unico vieta a due
/// sezioni di portare lo stesso numero. Quando succede, due difetti insieme — la lettura e il motore possono
/// ordinare in modo diverso (una mostrava un ordine e l'altro ne spostava un altro), e lo scambio dei due
/// numeri non cambia niente, cioè la freccia diventa un tasto che non fa nulla.</para>
/// </summary>
public class OrdineSezioniSpareggioTests : IAsyncLifetime
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

    /// <summary>Tre sorelle sotto un padre, tutte con lo stesso <c>Order</c>: è la condizione da provare.</summary>
    private async Task<(int Draft, int Padre, int[] Figlie)> TreSorelleAllaPariAsync()
    {
        var docId = await _db.Documents.Where(d => d.Type == DocumentType.Vipi).Select(d => d.Id).FirstAsync();
        var draft = await _repo.CreateDraftAsync(docId, authorUserId: 1);

        var padre = await _repo.AddSectionAsync(draft, null, "Padre", BlockSection.Other);
        var figlie = new List<int>();
        foreach (var t in new[] { "A", "B", "C" })
            figlie.Add(await _repo.AddSectionAsync(draft, padre, t, BlockSection.Other));

        foreach (var s in await _db.DocumentSections.Where(s => figlie.Contains(s.Id)).ToListAsync())
            s.Order = 1;
        await _db.SaveChangesAsync();

        return (draft, padre, figlie.ToArray());
    }

    /// <summary>La freccia sposta anche quando i numeri sono pari: prima non faceva niente.</summary>
    [Fact]
    public async Task La_freccia_muove_anche_a_parita_di_ordine()
    {
        var (draft, padre, figlie) = await TreSorelleAllaPariAsync();

        await _repo.MoveSectionAsync(figlie[2], -1);   // C su

        Assert.Equal(new[] { figlie[0], figlie[2], figlie[1] }, await LetteAsync(draft, padre));
    }

    /// <summary>E l'ordine che legge l'editor è quello che il motore usa per spostare: senza lo spareggio, la
    /// pagina poteva mostrare A, B, C e la freccia scambiare altre due.</summary>
    [Fact]
    public async Task Chi_legge_e_chi_sposta_vedono_lo_stesso_ordine()
    {
        var (draft, padre, figlie) = await TreSorelleAllaPariAsync();

        Assert.Equal(figlie, await LetteAsync(draft, padre));

        // Spostata l'ultima in cima, la lettura la trova in cima: i due ordini restano lo stesso ordine.
        await _repo.MoveSectionBeforeAsync(figlie[2], figlie[0]);
        Assert.Equal(new[] { figlie[2], figlie[0], figlie[1] }, await LetteAsync(draft, padre));
    }

    /// <summary>Le figlie come le legge l'editor (<c>LoadForEditAsync</c> → <c>EditableSection.Children</c>).</summary>
    private async Task<int[]> LetteAsync(int draftId, int padreId)
    {
        var docId = await _db.DocumentVersions.Where(v => v.Id == draftId).Select(v => v.DocumentId).FirstAsync();
        var doc = await _repo.LoadForEditAsync(docId) ?? throw new InvalidOperationException("documento non letto");
        var padre = doc.Sections.First(s => s.Id == padreId);
        return padre.Children.Select(c => c.Id).ToArray();
    }
}
