using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// doc 14 §3b — la riga «Effective from — AIRAC ####» che il seminatore piantava nelle vLOA se ne va, ma solo
/// quella. Il numero era il ciclo del giorno della creazione e non si aggiornava mai, mentre la scheda sopra
/// mostra quello della release mostrata: sull'archivio di sviluppo tutte e quattro le vLOA dicevano «AIRAC 2607»
/// e la LIBB↔LDZO era pubblicata al 2608 — due numeri diversi nella stessa pagina.
/// </summary>
public class VloaAiracRowCleanupTests : IAsyncLifetime
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

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>La tabella com'era scritta dal seminatore, parametrizzabile per le varianti.</summary>
    private static string Tabella(params string[][] righe) =>
        JsonSerializer.Serialize(new
        {
            columns = new[] { "Item", "Value" },
            unified = false,
            rows = righe.Select(r => new { cells = r }).ToArray(),
        });

    private async Task<ContentBlock> SeedVloaAsync(string bodyJson, DocumentType tipo = DocumentType.Vloa,
        string sectionKey = "validity", BlockFormat formato = BlockFormat.Table)
    {
        var doc = new Document { Type = tipo, Title = "vLOA — LIBB ↔ LDZO", Language = Language.En, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2607" };
        _db.Documents.Add(doc);
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2607" };
        _db.DocumentVersions.Add(ver);
        var sec = new DocumentSection
        {
            DocumentVersion = ver, Title = "Validity and Revision", Order = 1, Depth = 0,
            SectionKey = sectionKey, RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(sec);
        var blocco = new ContentBlock
        {
            DocumentVersion = ver, Section = sec, Order = 1, Format = formato,
            Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always,
            BodyJson = bodyJson, RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.ContentBlocks.Add(blocco);
        await _db.SaveChangesAsync();
        return blocco;
    }

    private static string[] Righe(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("cells")[0].GetString() ?? "").ToArray();

    [Fact]
    public async Task Toglie_la_riga_seminata_e_lascia_in_piedi_le_altre()
    {
        var b = await SeedVloaAsync(Tabella(
            new[] { "Effective from", "AIRAC 2607" },
            new[] { "Review cycle", "Bilateral, at least annually" },
            new[] { "Italian signatory", "LIBB CH / AOD" }));

        var tolte = await _manutenzione.ClearVloaSeededAiracRowAsync();

        Assert.Equal(1, tolte);
        await _db.Entry(b).ReloadAsync();
        Assert.Equal(new[] { "Review cycle", "Italian signatory" }, Righe(b.BodyJson!));
    }

    [Fact]
    public async Task E_idempotente()
    {
        await SeedVloaAsync(Tabella(
            new[] { "Effective from", "AIRAC 2607" },
            new[] { "Review cycle", "Bilateral, at least annually" }));

        Assert.Equal(1, await _manutenzione.ClearVloaSeededAiracRowAsync());
        Assert.Equal(0, await _manutenzione.ClearVloaSeededAiracRowAsync());
        Assert.Equal(0, await _manutenzione.ClearVloaSeededAiracRowAsync());
    }

    [Fact]
    public async Task Non_tocca_una_riga_che_l_editore_ha_riscritto()
    {
        // Il valore non ha più la forma seminata: è una frase di qualcuno. Toglierla sarebbe peggio del difetto.
        var b = await SeedVloaAsync(Tabella(
            new[] { "Effective from", "AIRAC 2607, salvo diverso accordo bilaterale" },
            new[] { "Review cycle", "Bilateral" }));

        Assert.Equal(0, await _manutenzione.ClearVloaSeededAiracRowAsync());
        await _db.Entry(b).ReloadAsync();
        Assert.Equal(2, Righe(b.BodyJson!).Length);
    }

    [Fact]
    public async Task Non_tocca_una_riga_con_un_altra_etichetta()
    {
        var b = await SeedVloaAsync(Tabella(
            new[] { "In vigore dal", "AIRAC 2607" },
            new[] { "Review cycle", "Bilateral" }));

        Assert.Equal(0, await _manutenzione.ClearVloaSeededAiracRowAsync());
        await _db.Entry(b).ReloadAsync();
        Assert.Equal(2, Righe(b.BodyJson!).Length);
    }

    [Fact]
    public async Task Non_tocca_le_vIPI_ne_le_altre_sezioni()
    {
        var vipi = await SeedVloaAsync(Tabella(new[] { "Effective from", "AIRAC 2607" }), tipo: DocumentType.Vipi);
        var altra = await SeedVloaAsync(Tabella(new[] { "Effective from", "AIRAC 2607" }), sectionKey: "purpose");

        Assert.Equal(0, await _manutenzione.ClearVloaSeededAiracRowAsync());
        await _db.Entry(vipi).ReloadAsync();
        await _db.Entry(altra).ReloadAsync();
        Assert.Single(Righe(vipi.BodyJson!));
        Assert.Single(Righe(altra.BodyJson!));
    }

    [Fact]
    public async Task Se_la_tabella_resta_senza_righe_il_blocco_se_ne_va()
    {
        // Una tabella a zero righe è un rettangolo vuoto in mezzo al documento.
        var b = await SeedVloaAsync(Tabella(new[] { "Effective from", "AIRAC 2607" }));

        Assert.Equal(1, await _manutenzione.ClearVloaSeededAiracRowAsync());
        Assert.Null(await _db.ContentBlocks.FirstOrDefaultAsync(x => x.Id == b.Id));
    }

    [Fact]
    public async Task Un_json_illeggibile_non_fa_cadere_il_passo()
    {
        // Non è compito di questa manutenzione riparare un blocco rotto: lo lascia stare e non solleva.
        var b = await SeedVloaAsync("{ questo non è json");

        Assert.Equal(0, await _manutenzione.ClearVloaSeededAiracRowAsync());
        await _db.Entry(b).ReloadAsync();
        Assert.Equal("{ questo non è json", b.BodyJson);
    }

    [Theory]
    [InlineData("AIRAC 2607")]
    [InlineData("AIRAC2607")]
    [InlineData("airac 2607")]
    [InlineData("AIRAC  2607")]
    public async Task Riconosce_le_forme_del_valore_seminato(string valore)
    {
        await SeedVloaAsync(Tabella(new[] { "Effective from", valore }, new[] { "Review cycle", "Bilateral" }));
        Assert.Equal(1, await _manutenzione.ClearVloaSeededAiracRowAsync());
    }
}
