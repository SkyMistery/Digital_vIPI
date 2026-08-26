using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Media;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Pulizia delle immagini non più usate. Il rischio di questa funzione non è lasciare spazzatura, è
/// <b>cancellare una foto ancora in uso</b>: succederebbe in silenzio e si vedrebbe solo aprendo un documento
/// pubblicato mesi dopo. Qui si fissano i quattro posti da cui un riferimento può arrivare, e il ricontrollo al
/// momento della cancellazione.
/// </summary>
public class MediaMaintenanceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfMediaMaintenance _manutenzione = default!;
    private DocumentVersion _ver = default!;
    private DocumentSection _sec = default!;

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? Get() => new(704798, "Tester", "LIRR", new[] { "IT-AOC" });
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _manutenzione = new EfMediaMaintenance(_db);

        var doc = new Document { Type = DocumentType.Vipi, Title = "vIPI di prova", Language = Language.It, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2607" };
        _ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2607", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(_ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        _sec = new DocumentSection { DocumentVersion = _ver, Title = "Sezione", Order = 1, Depth = 0, SectionKey = "custom", RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(_sec);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    // --- aiutanti ---

    /// Carica un'immagine vera passando dal deposito (così sha e misure sono quelli veri).
    private async Task<string> CaricaAsync(byte marcatore)
    {
        var store = new EfMediaStore(_db, new FakeUser(), Options.Create(new MediaOptions()));
        var png = new byte[25];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
        png[11] = 0x0D;
        "IHDR"u8.ToArray().CopyTo(png, 12);
        png[18] = 0x03; png[19] = 0x20;   // 800
        png[22] = 0x02; png[23] = 0x58;   // 600
        png[24] = marcatore;              // contenuti diversi ⇒ sha diversi
        return (await store.SaveAsync(new MemoryStream(png), $"foto{marcatore}.png")).Sha256;
    }

    private async Task BloccoImmagineAsync(string sha)
    {
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersion = _ver, Section = _sec, Order = _db.ContentBlocks.Count() + 1,
            Format = BlockFormat.Image, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            BodyJson = MediaRef.Serialize(new MediaRef(sha, "alt", 800, 600)),
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> OrfaniAsync() =>
        (await _manutenzione.AnalyzeAsync()).Orphans.Select(o => o.Sha256).ToList();

    // --- i casi che contano ---

    [Fact]
    public async Task Immagine_mai_citata_e_orfana_e_il_quadro_dice_quanto_si_recupera()
    {
        var sha = await CaricaAsync(1);

        var report = await _manutenzione.AnalyzeAsync();

        Assert.Equal(1, report.TotalCount);
        Assert.Equal(25, report.TotalBytes);
        var orfano = Assert.Single(report.Orphans);
        Assert.Equal(sha, orfano.Sha256);
        Assert.Equal("foto1.png", orfano.FileName);
        Assert.Equal(25, report.ReclaimableBytes);
    }

    [Fact]
    public async Task Immagine_citata_da_una_bozza_non_e_orfana()
    {
        // La bozza non è pubblicata: è la foto che qualcuno sta scrivendo ADESSO.
        var sha = await CaricaAsync(1);
        await BloccoImmagineAsync(sha);

        Assert.Empty(await OrfaniAsync());
    }

    [Fact]
    public async Task Immagine_citata_da_una_sezione_extra_di_aeroporto_non_e_orfana()
    {
        var sha = await CaricaAsync(1);
        var acc = new Acc { Code = "LIBB", Name = "Brindisi", CountryPrefix = "LI" };
        var apt = new Airport { Acc = acc, Icao = "LIBD", Name = "Bari" };
        _db.Accs.Add(acc); _db.Airports.Add(apt);
        await _db.SaveChangesAsync();

        var body = ExtraBlocks.Serialize(new List<ExtraBlock>
        {
            new() { Format = BlockFormat.Image, ImageJson = MediaRef.Serialize(new MediaRef(sha)), Text = "didascalia" },
        });
        Assert.Contains(sha, body);   // se salta qui il difetto è nella serializzazione, non nella pulizia

        _db.AirportExtraSections.Add(new AirportExtraSection { AirportId = apt.Id, Title = "Hot spot", Order = 1, Body = body });
        await _db.SaveChangesAsync();

        Assert.Empty(await OrfaniAsync());
    }

    [Fact]
    public async Task Immagine_citata_solo_da_una_release_pubblicata_non_e_orfana()
    {
        // Il caso che rende la funzione pericolosa: nel documento di lavoro il blocco non c'è più, ma la
        // fotografia congelata della release continua a mostrarla.
        var sha = await CaricaAsync(1);
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.AccVipi, TargetKey = "LIBB", VersionNumber = 1,
            ReleaseAiracCycle = "2606", ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-30),
            Status = ReleaseStatus.Effective, CreatedByUserId = 1, CreatedUtc = DateTime.UtcNow.AddDays(-30),
            // forma reale: il BodyJson del blocco è una stringa dentro il payload
            PayloadJson = $"{{\"Doc\":{{\"Blocks\":[{{\"BodyJson\":\"{{\\\"mediaId\\\":\\\"{sha}\\\"}}\"}}]}}}}",
        });
        await _db.SaveChangesAsync();

        Assert.Empty(await OrfaniAsync());
    }

    [Fact]
    public async Task Immagine_citata_da_un_blocco_condiviso_non_e_orfana()
    {
        // Oggi nessuno crea SharedBlock, ma il modello li prevede (ContentBlock.SharedBlockId) e portano
        // Format + BodyJson come i blocchi normali: se un domani si usassero, senza questa sorgente la pulizia
        // cancellerebbe la foto di un contenuto condiviso ancora in uso.
        var sha = await CaricaAsync(1);
        _db.SharedBlocks.Add(new SharedBlock
        {
            Key = "logo-divisione", Title = "Logo", Format = BlockFormat.Image,
            BodyJson = MediaRef.Serialize(new MediaRef(sha, "Logo", 200, 200)),
        });
        await _db.SaveChangesAsync();

        Assert.Empty(await OrfaniAsync());
    }

    [Fact]
    public async Task Immagine_usata_da_due_blocchi_resta_finche_non_spariscono_entrambi()
    {
        var sha = await CaricaAsync(1);
        await BloccoImmagineAsync(sha);
        await BloccoImmagineAsync(sha);

        _db.ContentBlocks.Remove(await _db.ContentBlocks.FirstAsync());
        await _db.SaveChangesAsync();
        Assert.Empty(await OrfaniAsync());          // c'è ancora l'altro blocco

        _db.ContentBlocks.RemoveRange(await _db.ContentBlocks.ToListAsync());
        await _db.SaveChangesAsync();
        Assert.Equal(new[] { sha }, await OrfaniAsync());
    }

    [Fact]
    public async Task Cancella_solo_gli_sha_indicati()
    {
        var orfana = await CaricaAsync(1);
        var altra = await CaricaAsync(2);

        var cancellati = await _manutenzione.DeleteOrphansAsync(new[] { orfana });

        Assert.Equal(1, cancellati);
        Assert.Equal(new[] { altra }, await _db.MediaAssets.Select(m => m.Sha256).ToListAsync());
    }

    [Fact]
    public async Task Uno_sha_tornato_in_uso_fra_analisi_e_clic_non_viene_cancellato()
    {
        var sha = await CaricaAsync(1);
        var elenco = await OrfaniAsync();            // l'utente vede l'elenco…
        Assert.Single(elenco);

        await BloccoImmagineAsync(sha);              // …e nel frattempo qualcuno la usa

        Assert.Equal(0, await _manutenzione.DeleteOrphansAsync(elenco));
        Assert.Equal(1, await _db.MediaAssets.CountAsync());
    }

    // --- pulizia automatica alla cancellazione + quota per documento ---

    private EfEditingRepository Editing() =>
        new(_db, new Vipi.Domain.Services.AiracService(), _manutenzione);

    [Fact]
    public async Task Cancellare_il_blocco_immagine_libera_subito_la_foto()
    {
        var sha = await CaricaAsync(1);
        await BloccoImmagineAsync(sha);
        var blocco = await _db.ContentBlocks.FirstAsync();

        await Editing().DeleteBlockAsync(blocco.Id);

        Assert.Equal(0, await _db.MediaAssets.CountAsync());
    }

    [Fact]
    public async Task Cancellare_il_blocco_NON_tocca_una_foto_usata_anche_altrove()
    {
        // Il caso che rende pericolosa la cancellazione automatica: due blocchi, stessa foto.
        var sha = await CaricaAsync(1);
        await BloccoImmagineAsync(sha);
        await BloccoImmagineAsync(sha);
        var primo = await _db.ContentBlocks.OrderBy(b => b.Id).FirstAsync();

        await Editing().DeleteBlockAsync(primo.Id);

        Assert.Equal(1, await _db.MediaAssets.CountAsync());
    }

    [Fact]
    public async Task Cancellare_il_blocco_NON_tocca_una_foto_citata_da_una_release()
    {
        // Nel documento di lavoro sparisce, ma la vIPI gia' pubblicata continua a mostrarla.
        var sha = await CaricaAsync(1);
        await BloccoImmagineAsync(sha);
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.AccVipi, TargetKey = "LIBB", VersionNumber = 1,
            ReleaseAiracCycle = "2606", ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-30),
            Status = ReleaseStatus.Effective, CreatedByUserId = 1, CreatedUtc = DateTime.UtcNow.AddDays(-30),
            PayloadJson = MediaRef.Serialize(new MediaRef(sha)),
        });
        await _db.SaveChangesAsync();
        var blocco = await _db.ContentBlocks.FirstAsync();

        await Editing().DeleteBlockAsync(blocco.Id);

        Assert.Equal(1, await _db.MediaAssets.CountAsync());
    }

    [Fact]
    public async Task Cancellare_la_sezione_libera_le_foto_dell_intero_sottoalbero()
    {
        var shaPadre = await CaricaAsync(1);
        var shaFiglia = await CaricaAsync(2);
        await BloccoImmagineAsync(shaPadre);

        var figlia = new DocumentSection
        {
            DocumentVersion = _ver, ParentSectionId = _sec.Id, Title = "Sotto-sezione", Order = 1, Depth = 1,
            SectionKey = "custom", RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(figlia);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersion = _ver, Section = figlia, Order = 1, Format = BlockFormat.Image,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            BodyJson = MediaRef.Serialize(new MediaRef(shaFiglia)), RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        await Editing().DeleteSectionAsync(_sec.Id);

        Assert.Equal(0, await _db.MediaAssets.CountAsync());
    }

    [Fact]
    public async Task La_quota_conta_i_byte_delle_immagini_del_documento()
    {
        var sha = await CaricaAsync(1);
        await BloccoImmagineAsync(sha);

        var documentId = await _db.DocumentVersions.Where(v => v.Id == _ver.Id).Select(v => v.DocumentId).FirstAsync();

        Assert.Equal(25, await _manutenzione.DocumentImageBytesAsync(documentId));
        Assert.Equal(0, await _manutenzione.DocumentImageBytesAsync(documentId + 999));   // documento inesistente
    }

    [Fact]
    public async Task La_stessa_foto_in_due_blocchi_pesa_una_volta_sola()
    {
        // La quota misura lo SPAZIO occupato, non quante volte la si mostra: nel deposito e' una riga sola.
        var sha = await CaricaAsync(1);
        await BloccoImmagineAsync(sha);
        await BloccoImmagineAsync(sha);

        var documentId = await _db.DocumentVersions.Where(v => v.Id == _ver.Id).Select(v => v.DocumentId).FirstAsync();

        Assert.Equal(25, await _manutenzione.DocumentImageBytesAsync(documentId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("non-uno-sha")]
    public async Task Sha_malformati_non_cancellano_niente(string sha)
    {
        await CaricaAsync(1);

        Assert.Equal(0, await _manutenzione.DeleteOrphansAsync(new[] { sha }));
        Assert.Equal(1, await _db.MediaAssets.CountAsync());
    }
}
