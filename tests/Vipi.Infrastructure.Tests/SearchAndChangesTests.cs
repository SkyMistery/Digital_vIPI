using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Ricerca full-text + "Cosa è cambiato" sul DB seedato.</summary>
public class SearchAndChangesTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSearchRepository _search = default!;
    private EfChangesRepository _changes = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);
        await RomaAirportSeed.SeedAsync(_db);
        await RomaVloaSeed.SeedAsync(_db);
        var releases = TestReleaseTargets.ReleaseRepo(_db);
        _search = new EfSearchRepository(_db, TestReleaseTargets.Registry(_db), TestReleaseTargets.Routes(), releases);
        _changes = new EfChangesRepository(_db, TestReleaseTargets.Registry(_db), TestReleaseTargets.Routes(), releases);

        // Visibilità pubblica = release AIRAC effettiva (doc 10 §3f): senza, gli indici non devono mostrare nulla.
        // Il fixture rappresenta quindi lo stato «tutto pubblicato»; i test del gate tolgono ciò che serve.
        await PublishAllAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>Dà una release effettiva a ogni documento gestito (payload irrilevante: il gate guarda l'esistenza).</summary>
    private async Task PublishAllAsync()
    {
        var admin = TestReleaseTargets.AdminRepo(_db);
        var releases = TestReleaseTargets.ReleaseRepo(_db);
        var cycle = new AiracService().GetCycle(DateTime.UtcNow);
        foreach (var d in await admin.ListAsync())
            await releases.SaveReleaseAsync(d.ReleaseTarget, d.ReleaseKey, cycle, DateTime.UtcNow.AddMinutes(-1),
                "{}", createdByUserId: 1, note: null);
    }

    [Fact]
    public async Task Search_Finds_Cop_In_Vipi()
    {
        var hits = await _search.SearchAsync("VALMA", SearchScope.All, 50);
        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Url.Contains("/vipi") && h.Snippet.Contains("VALMA", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_Scope_Filters()
    {
        // ESEBA è nei CoP della vLOA → deve comparire solo nello scope vLOA, non in vIPI.
        var vloa = await _search.SearchAsync("ESEBA", SearchScope.Vloa, 50);
        Assert.NotEmpty(vloa);
        Assert.All(vloa, h => Assert.Contains("/vloa", h.Url));

        var vipi = await _search.SearchAsync("ESEBA", SearchScope.Vipi, 50);
        Assert.DoesNotContain(vipi, h => h.Url.Contains("/vloa"));
    }

    [Fact]
    public async Task Search_NoMatch_Empty()
    {
        var hits = await _search.SearchAsync("zzxxqqnotfound", SearchScope.All, 50);
        Assert.Empty(hits);
    }

    /// <summary>
    /// Dall'11 agosto 2026 il filtro sul corpo dei blocchi sta nel <b>database</b>, non in memoria: prima
    /// ogni ricerca leggeva l'intero contenuto pubblicato — Body e BodyJson, cioè poligoni AoR e tabelle di
    /// configurazione — e poi buttava via quasi tutto.
    ///
    /// <para>La cosa che il cambio poteva rompere in silenzio è proprio questa: <c>LIKE</c> segue la
    /// collation, e in produzione la collation è <c>utf8mb4_uca1400_as_cs</c>, cioè <b>case-sensitive</b>.
    /// Per questo il filtro è scritto con <c>ToLower()</c> su entrambi i lati. Cercare in minuscolo un testo
    /// scritto in maiuscolo deve continuare a funzionare — è come cerca chiunque.</para>
    /// </summary>
    [Theory]
    [InlineData("VALMA")]
    [InlineData("valma")]
    [InlineData("VaLmA")]
    public async Task Search_Ignora_Le_Maiuscole_Anche_Filtrando_Nel_Database(string termine)
    {
        var hits = await _search.SearchAsync(termine, SearchScope.All, 50);
        Assert.Contains(hits, h => h.Snippet.Contains("VALMA", System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Il rovescio: i blocchi che non contengono il termine non devono nemmeno uscire dal database. Si misura
    /// dall'esito — un termine presente in UN blocco solo non può produrre più di un risultato di corpo — non
    /// dal SQL, che è dettaglio del provider.
    /// </summary>
    [Fact]
    public async Task Search_Non_Riporta_Blocchi_Che_Non_Contengono_Il_Termine()
    {
        var section = await _db.DocumentSections.FirstAsync();
        _db.ContentBlocks.Add(new Vipi.Domain.Entities.ContentBlock
        {
            DocumentVersionId = section.DocumentVersionId,
            SectionId = section.Id,
            Order = 9000,
            Format = Vipi.Domain.BlockFormat.Prose,
            Body = "Parola rarissima: xyzzyplugh.",
        });
        await _db.SaveChangesAsync();

        var hits = await _search.SearchAsync("xyzzyplugh", SearchScope.All, 50);

        Assert.Single(hits);
        Assert.Contains("xyzzyplugh", hits[0].Snippet, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Un blocco immagine ha per testo il suo alternativo e la didascalia. Il BodyJson porta lo sha: se finisse
    /// nell'indice, cercare una sequenza qualsiasi pescherebbe immagini a caso e il risultato mostrerebbe JSON.
    /// </summary>
    [Fact]
    public async Task Search_Indexes_Image_Alt_And_Caption_Not_The_Json()
    {
        const string sha = "beef56789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0";
        var section = await _db.DocumentSections.FirstAsync();
        _db.ContentBlocks.Add(new Vipi.Domain.Entities.ContentBlock
        {
            DocumentVersionId = section.DocumentVersionId,
            SectionId = section.Id,
            Order = 99,
            Format = Vipi.Domain.BlockFormat.Image,
            Tier = Vipi.Domain.BlockTier.Extended,
            Visibility = Vipi.Domain.BlockVisibility.Always,
            Body = "Vista del piazzale",
            BodyJson = MediaRef.Serialize(new MediaRef(sha, "Hangar sud", 1600, 900)),
        });
        await _db.SaveChangesAsync();

        var perAlt = await _search.SearchAsync("Hangar sud", SearchScope.All, 50);
        Assert.Contains(perAlt, h => h.Snippet.Contains("Hangar sud"));
        Assert.DoesNotContain(perAlt, h => h.Snippet.Contains("mediaId"));

        var perDidascalia = await _search.SearchAsync("piazzale", SearchScope.All, 50);
        Assert.Contains(perDidascalia, h => h.Snippet.Contains("Vista del piazzale"));

        // Lo sha non è testo: cercarne un pezzo non deve pescare l'immagine.
        Assert.Empty(await _search.SearchAsync("beef5678", SearchScope.All, 50));
    }

    [Fact]
    public async Task Changed_Lists_Documents_In_Current_Cycle()
    {
        var cycle = new AiracService().GetCycle(System.DateTime.UtcNow);
        var rows = await _changes.ListChangedAsync(cycle);

        Assert.NotEmpty(rows);
        Assert.Contains(rows, r => r.DocTitle.Contains("Roma ACC"));
        // versioni uniche dei seed → "Nuovo" (v1), prev counts a 0
        Assert.All(rows, r => Assert.True(r.CurrBlocks > 0));
    }

    [Fact]
    public async Task Changed_OtherCycle_Empty()
    {
        var rows = await _changes.ListChangedAsync("9999");
        Assert.Empty(rows);
    }

    // ---- doc 13 §3e: la rotta di un risultato la dà il registry, non un if scritto a mano ----

    /// <summary>Crea e pubblica il documento dell'APP non remotizzato LIRP_APP con una sezione riconoscibile.</summary>
    private async Task<int> SeedPublishedAppDocumentAsync()
    {
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var sectorId = await _db.Sectors.Where(x => x.Callsign == "LIRP_APP").Select(x => x.Id).FirstAsync();
        var docId = await editing.EnsureVipiDocumentAsync(sectorId, "vIPI Pisa Avvicinamento", Vipi.Domain.Language.It,
            new[] { ("custom:pisatok", "Consegne particolari PISATOKEN") }, authorUserId: 1);
        var versionId = await _db.DocumentVersions.Where(v => v.DocumentId == docId).Select(v => v.Id).FirstAsync();
        await editing.PublishAsync(versionId, actorUserId: 1, note: null);
        await TestReleaseTargets.ReleaseRepo(_db).SaveReleaseAsync(
            Vipi.Domain.ReleaseTargetType.App, "LIRP_APP", new AiracService().GetCycle(DateTime.UtcNow),
            DateTime.UtcNow.AddMinutes(-1), "{}", createdByUserId: 1, note: null);
        return docId;
    }

    [Fact]
    public async Task An_App_document_points_at_its_own_page_not_at_the_Acc_vipi()
    {
        // Il difetto: la risoluzione scritta a mano distingueva solo «aeroporto» da «tutto il resto», quindi ogni
        // documento di APP standalone puntava a /services/vsop/{acc}/vipi — la vIPI di ACC, un altro documento.
        await SeedPublishedAppDocumentAsync();

        var hits = await _search.SearchAsync("PISATOKEN", SearchScope.All, 50);

        var hit = Assert.Single(hits);
        Assert.Equal("/services/vsop/lirr/apps/vipi?app=LIRP_APP", hit.Url.Split('#')[0]);
    }

    [Fact]
    public async Task The_App_scope_separates_the_approaches_from_the_Acc_vipi()
    {
        await SeedPublishedAppDocumentAsync();

        var app = await _search.SearchAsync("PISATOKEN", SearchScope.App, 50);
        Assert.NotEmpty(app);
        Assert.All(app, h => Assert.Contains("/apps/vipi", h.Url));

        // …e nello scope «vIPI» (le ACC) non compare più mescolato.
        var acc = await _search.SearchAsync("PISATOKEN", SearchScope.Vipi, 50);
        Assert.Empty(acc);
    }

    [Fact]
    public async Task Changed_lists_an_App_document_with_its_own_route()
    {
        await SeedPublishedAppDocumentAsync();

        var rows = await _changes.ListChangedAsync(new AiracService().GetCycle(DateTime.UtcNow));

        var row = Assert.Single(rows, r => r.DocTitle.Contains("Pisa"));
        Assert.Equal("/services/vsop/lirr/apps/vipi?app=LIRP_APP", row.Url);
    }

    // ---- doc 13 §3f: l'indice vede quello che vede la pagina ----

    [Fact]
    public async Task A_hidden_document_disappears_from_search_and_from_changed()
    {
        await SeedPublishedAppDocumentAsync();
        Assert.NotEmpty(await _search.SearchAsync("PISATOKEN", SearchScope.All, 50));

        var doc = await _db.Documents.FirstAsync(d => d.Title.Contains("Pisa"));
        doc.IsHidden = true;
        await _db.SaveChangesAsync();

        Assert.Empty(await _search.SearchAsync("PISATOKEN", SearchScope.All, 50));
        Assert.DoesNotContain(await _changes.ListChangedAsync(new AiracService().GetCycle(DateTime.UtcNow)),
            r => r.DocTitle.Contains("Pisa"));
    }

    [Fact]
    public async Task A_document_without_an_effective_release_is_not_indexed()
    {
        // La pagina di un documento senza release dice «non disponibile»: l'indice non deve servirne il contenuto,
        // e nemmeno linkarlo da «Cosa è cambiato».
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var sectorId = await _db.Sectors.Where(x => x.Callsign == "LIRP_APP").Select(x => x.Id).FirstAsync();
        var docId = await editing.EnsureVipiDocumentAsync(sectorId, "vIPI Pisa Avvicinamento", Vipi.Domain.Language.It,
            new[] { ("custom:pisatok", "Consegne particolari PISATOKEN") }, authorUserId: 1);
        var versionId = await _db.DocumentVersions.Where(v => v.DocumentId == docId).Select(v => v.Id).FirstAsync();
        await editing.PublishAsync(versionId, actorUserId: 1, note: null);   // versione pubblicata, MA nessuna release

        Assert.Empty(await _search.SearchAsync("PISATOKEN", SearchScope.All, 50));
        Assert.DoesNotContain(await _changes.ListChangedAsync(new AiracService().GetCycle(DateTime.UtcNow)),
            r => r.DocTitle.Contains("Pisa"));
    }

    [Fact]
    public async Task A_hidden_section_is_not_indexed_and_neither_is_what_is_under_it()
    {
        await SeedPublishedAppDocumentAsync();
        var section = await _db.DocumentSections.FirstAsync(x => x.Title.Contains("PISATOKEN"));

        // Una sotto-sezione con contenuto proprio: nascondendo il padre sparisce anche lei.
        var child = new Vipi.Domain.Entities.DocumentSection
        {
            DocumentVersionId = section.DocumentVersionId, ParentSectionId = section.Id,
            Title = "Dettaglio", Order = 1, Depth = 1, SectionKey = SectionKeys.NewCustom(),
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(child);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new Vipi.Domain.Entities.ContentBlock
        {
            DocumentVersionId = section.DocumentVersionId, SectionId = child.Id, Order = 1,
            Format = Vipi.Domain.BlockFormat.Prose, Tier = Vipi.Domain.BlockTier.Extended,
            Visibility = Vipi.Domain.BlockVisibility.Always, Body = "SOTTOTOKEN",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        Assert.NotEmpty(await _search.SearchAsync("SOTTOTOKEN", SearchScope.All, 50));

        section.IsHidden = true;
        await _db.SaveChangesAsync();

        Assert.Empty(await _search.SearchAsync("PISATOKEN", SearchScope.All, 50));
        Assert.Empty(await _search.SearchAsync("SOTTOTOKEN", SearchScope.All, 50));
    }
}
