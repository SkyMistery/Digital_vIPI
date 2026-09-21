using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il gruppo APP della vIPI di ACC si scrive come l'APP non remotizzato — 21 settembre 2026, committente:
/// «Gestione del traffico» con IFR e VFR a blocchi sopra i Coordinamenti, «Tecnica operativa» sotto.
/// Sulle vIPI già scritte: il VFR, figlio del blocco, scende nel contenitore, e il resto lo aggiunge il catalogo.
/// </summary>
public class AccGruppoAppTrafficoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentMaintenance _manutenzione = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _manutenzione = new EfDocumentMaintenance(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static readonly string[] GruppoDiPrima =
    {
        "separations", "configurations", "aor", "frequencies", "minima", "vfr", "coordination", "regulated",
        "operationaltechnique", "validity",
    };

    /// <summary>Una vIPI di ACC nella forma di prima: il blocco Aerovia completo e un gruppo APP col VFR figlio.</summary>
    private async Task<(DocumentVersion Ver, DocumentSection Gruppo)> AccVecchiaAsync(string? vfrJson = null)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI LIBB", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2609",
        };
        _db.Documents.Add(doc);
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft,
            CreatedByUserId = 0, CreatedUtc = DateTime.UtcNow, AiracCycle = "2609",
        };
        _db.DocumentVersions.Add(ver);
        var aerovia = Sezione(ver, null, SectionKeys.AccBloccoAerovia, 1);
        var gruppo = Sezione(ver, null, SectionKeys.AccBloccoApp, 2);
        var n = 0;
        foreach (var d in SectionCatalog.For(SectionProfile.AccAerovia).OrderBy(d => d.Order))
            Sezione(ver, aerovia, d.Key, ++n);
        n = 0;
        foreach (var k in GruppoDiPrima) Sezione(ver, gruppo, k, ++n);
        await _db.SaveChangesAsync();

        var vfr = await _db.DocumentSections.SingleAsync(x => x.ParentSectionId == gruppo.Id && x.SectionKey == "vfr");
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = vfr.Id, Order = 1, Format = BlockFormat.Table,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, BodyJson = vfrJson,
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();
        return (ver, gruppo);
    }

    private DocumentSection Sezione(DocumentVersion ver, DocumentSection? padre, string key, int ordine)
    {
        var s = new DocumentSection
        {
            DocumentVersion = ver, ParentSection = padre, Title = key, Order = ordine,
            Depth = padre is null ? 0 : padre.Depth + 1, SectionKey = key, RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(s);
        return s;
    }

    private List<string> Figlie(int padreId) => _db.DocumentSections.AsNoTracking()
        .Where(x => x.ParentSectionId == padreId).OrderBy(x => x.Order).Select(x => x.SectionKey).ToList();

    [Fact]
    public async Task Il_VFR_del_gruppo_scende_nella_gestione_del_traffico_e_il_gruppo_prende_l_indice_del_catalogo()
    {
        var (_, gruppo) = await AccVecchiaAsync();

        Assert.Equal(1, await _manutenzione.ReparentAppTrafficManagementAsync());
        await _manutenzione.AddMissingCatalogSectionsAsync();

        Assert.Equal(SectionCatalog.For(SectionProfile.AccAppBlock).OrderBy(d => d.Order).Select(d => d.Key),
            Figlie(gruppo.Id));
        var traffico = await _db.DocumentSections.AsNoTracking()
            .SingleAsync(x => x.ParentSectionId == gruppo.Id && x.SectionKey == SectionKeys.TrafficManagement);
        Assert.Equal("Gestione del traffico", traffico.Title);
        Assert.Equal(1, traffico.Depth);
        Assert.Equal(new[] { SectionKeys.TrafficManagementIfr, "vfr" }, Figlie(traffico.Id));
        // 🔴 Un VFR SOLO: contato fra le sole figlie del blocco sarebbe sembrato mancante, e ne nasceva un secondo.
        Assert.Equal(1, await _db.DocumentSections.CountAsync(x => x.SectionKey == "vfr"));
        Assert.All(await _db.DocumentSections.AsNoTracking().Where(x => x.ParentSectionId == traffico.Id).ToListAsync(),
            x => Assert.Equal(2, x.Depth));
    }

    [Fact]
    public async Task Il_contenuto_della_vecchia_tabella_VFR_del_gruppo_non_si_perde()
    {
        var json = JsonSerializer.Serialize(new AppVfrContent("Intro VFR", new[] { new AppVfrRow("Ingresso CTR", "Via punto N") }));
        var (_, gruppo) = await AccVecchiaAsync(json);

        await _manutenzione.ReparentAppTrafficManagementAsync();

        var vfr = await _db.DocumentSections.AsNoTracking().SingleAsync(x => x.SectionKey == "vfr");
        var blocchi = await _db.ContentBlocks.AsNoTracking().Where(b => b.SectionId == vfr.Id).OrderBy(b => b.Order).ToListAsync();
        Assert.Equal(new[] { BlockFormat.Prose, BlockFormat.Table }, blocchi.Select(b => b.Format));
        Assert.Equal("Intro VFR", blocchi[0].Body);
        var (_, righe) = TabellaGenerica.Leggi(blocchi[1].BodyJson);
        Assert.Equal(new[] { "Ingresso CTR", "Via punto N" }, Assert.Single(righe));
    }

    [Fact]
    public async Task Il_passo_e_idempotente_e_non_tocca_il_blocco_Aerovia()
    {
        var (ver, gruppo) = await AccVecchiaAsync();
        var aerovia = await _db.DocumentSections.AsNoTracking()
            .SingleAsync(x => x.DocumentVersionId == ver.Id && x.SectionKey == SectionKeys.AccBloccoAerovia);
        var primaAerovia = Figlie(aerovia.Id);

        await _manutenzione.ReparentAppTrafficManagementAsync();
        await _manutenzione.AddMissingCatalogSectionsAsync();
        var dopo = Figlie(gruppo.Id);

        Assert.Equal(0, await _manutenzione.ReparentAppTrafficManagementAsync());
        Assert.Equal(0, await _manutenzione.AddMissingCatalogSectionsAsync());
        Assert.Equal(dopo, Figlie(gruppo.Id));
        Assert.Equal(primaAerovia, Figlie(aerovia.Id));
    }
}
