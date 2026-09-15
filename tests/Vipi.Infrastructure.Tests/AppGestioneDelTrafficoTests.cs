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
/// La «Gestione del traffico» dell'APP non remotizzato — 15 settembre 2026, committente.
///
/// <para>Il VFR, che era una radice resa dalla pagina, scende dentro «Gestione del traffico» accanto a «IFR»,
/// e sotto i Coordinamenti arriva «Tecnica operativa». Sui documenti già scritti serve un passo apposta: il
/// catalogo decide la struttura solo alla nascita, e a mano una sezione non cambia padre.</para>
/// </summary>
public class AppGestioneDelTrafficoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentMaintenance _manutenzione = default!;
    private Acc _acc = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _manutenzione = new EfDocumentMaintenance(_db);
        _acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(_acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>Una vIPI d'APP nella forma di prima: dieci radici, il VFR sesto col suo segnaposto.</summary>
    private async Task<DocumentVersion> AppVecchioAsync(string callsign, bool standalone = true, string? vfrJson = null)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = $"vIPI {callsign}", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2609",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.Sectors.Add(new Sector
        {
            Acc = _acc, Callsign = callsign, Name = callsign, Type = SectorType.App, Kind = SectorKind.Airport,
            ApproachKind = standalone ? ApproachKind.Standalone : ApproachKind.Remotized, IsActive = true,
            DocumentId = doc.Id, IsPrimary = true,
        });
        var ver = new DocumentVersion
        {
            DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Draft,
            CreatedByUserId = 0, CreatedUtc = DateTime.UtcNow, AiracCycle = "2609",
        };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();

        var ordine = 0;
        foreach (var (k, t) in new[]
        {
            ("separations", "Separazioni"), ("configurations", "Configurazioni"), ("aor", "AOR"),
            ("frequencies", "Frequenze"), ("minima", "MRVA"), ("vfr", "VFR"), ("coordination", "Coordinamenti"),
            ("regulated", "Aree regolamentate"), ("operationaltechnique", "Procedure generali"),
            ("validity", "Validità e revisione"),
        })
        {
            _db.DocumentSections.Add(new DocumentSection
            {
                DocumentVersionId = ver.Id, Title = t, Order = ++ordine, Depth = 0, SectionKey = k,
                RowVersion = Guid.NewGuid().ToByteArray(),
            });
        }
        await _db.SaveChangesAsync();

        var vfr = await _db.DocumentSections.SingleAsync(x => x.DocumentVersionId == ver.Id && x.SectionKey == "vfr");
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = vfr.Id, Order = 1, Format = BlockFormat.Table,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, BodyJson = vfrJson,
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();
        return ver;
    }

    private List<string> Radici(DocumentVersion ver) => _db.DocumentSections.AsNoTracking()
        .Where(x => x.DocumentVersionId == ver.Id && x.ParentSectionId == null)
        .OrderBy(x => x.Order).Select(x => x.SectionKey).ToList();

    [Fact]
    public async Task Il_VFR_scende_nella_gestione_del_traffico_e_l_indice_diventa_quello_del_catalogo()
    {
        var ver = await AppVecchioAsync("LIRN_APP");

        Assert.Equal(1, await _manutenzione.ReparentAppTrafficManagementAsync());
        await _manutenzione.AddMissingCatalogSectionsAsync();

        Assert.Equal(SectionCatalog.For(SectionProfile.App).OrderBy(d => d.Order).Select(d => d.Key), Radici(ver));

        var traffico = await _db.DocumentSections.AsNoTracking()
            .SingleAsync(x => x.DocumentVersionId == ver.Id && x.SectionKey == SectionKeys.TrafficManagement);
        Assert.Equal("Gestione del traffico", traffico.Title);
        var figli = await _db.DocumentSections.AsNoTracking()
            .Where(x => x.ParentSectionId == traffico.Id).OrderBy(x => x.Order).ToListAsync();
        Assert.Equal(new[] { SectionKeys.TrafficManagementIfr, "vfr" }, figli.Select(x => x.SectionKey));
        Assert.Equal(new[] { 1, 2 }, figli.Select(x => x.Order));
        Assert.All(figli, x => Assert.Equal(1, x.Depth));

        // Il segnaposto vuoto se ne va: sulla sezione a blocchi sarebbe una tabella vuota da compilare.
        var vfr = figli[1];
        Assert.False(await _db.ContentBlocks.AnyAsync(b => b.SectionId == vfr.Id));
    }

    [Fact]
    public async Task Il_contenuto_della_vecchia_tabella_VFR_non_si_perde()
    {
        var json = JsonSerializer.Serialize(new AppVfrContent("Intro VFR", new[] { new AppVfrRow("Ingresso CTR", "Via punto N") }));
        var ver = await AppVecchioAsync("LIRN_APP", vfrJson: json);

        await _manutenzione.ReparentAppTrafficManagementAsync();

        var vfr = await _db.DocumentSections.AsNoTracking().SingleAsync(x => x.DocumentVersionId == ver.Id && x.SectionKey == "vfr");
        var blocchi = await _db.ContentBlocks.AsNoTracking().Where(b => b.SectionId == vfr.Id).OrderBy(b => b.Order).ToListAsync();
        Assert.Equal(2, blocchi.Count);
        Assert.Equal(BlockFormat.Prose, blocchi[0].Format);
        Assert.Equal("Intro VFR", blocchi[0].Body);
        Assert.Equal(BlockFormat.Table, blocchi[1].Format);
        Assert.True(SectionPayload.EEditoriale(blocchi[1].BodyJson));   // tabella generica, non più payload
        var (colonne, righe) = TabellaGenerica.Leggi(blocchi[1].BodyJson);
        Assert.Equal(new[] { "Situazione", "Procedura" }, colonne);
        Assert.Equal(new[] { "Ingresso CTR", "Via punto N" }, Assert.Single(righe));
    }

    [Fact]
    public async Task Il_passo_e_idempotente()
    {
        var ver = await AppVecchioAsync("LIRN_APP");
        Assert.Equal(1, await _manutenzione.ReparentAppTrafficManagementAsync());
        await _manutenzione.AddMissingCatalogSectionsAsync();
        var prima = Radici(ver);

        Assert.Equal(0, await _manutenzione.ReparentAppTrafficManagementAsync());
        Assert.Equal(0, await _manutenzione.AddMissingCatalogSectionsAsync());
        Assert.Equal(prima, Radici(ver));
    }

    [Fact]
    public async Task Un_APP_che_non_e_non_remotizzato_non_si_tocca()
    {
        var ver = await AppVecchioAsync("LIRF_APP", standalone: false);

        Assert.Equal(0, await _manutenzione.ReparentAppTrafficManagementAsync());
        Assert.Contains("vfr", Radici(ver));
    }
}
