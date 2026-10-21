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
/// Le note delle tabelle delle aree diventano paragrafi (S123b, 21 ottobre 2026, committente): stanno nel JSON del
/// payload della sezione — gli spazi aerei dell'AoR e le aree di lavoro e BOAT del vSOP militare — e il trasloco ne fa
/// un paragrafo, una riga per volume o area, riscrivendo il JSON senza.
/// </summary>
public class NoteDelleAreeTests : IAsyncLifetime
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

    /// <summary>Un documento con una versione e una sezione col suo payload; torna la sezione.</summary>
    private async Task<DocumentSection> SezioneAsync(string chiave, string payload)
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = "doc", Language = Language.It, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2611" };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var ver = new DocumentVersion { DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Draft, CreatedByUserId = 0, CreatedUtc = DateTime.UtcNow, AiracCycle = "2611" };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();
        doc.CurrentVersionId = ver.Id;
        var s = new DocumentSection { DocumentVersionId = ver.Id, Title = chiave, Order = 3, Depth = 0, SectionKey = chiave, RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(s);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersionId = ver.Id, SectionId = s.Id, Order = 1, Format = BlockFormat.Table, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, BodyJson = payload, RowVersion = Guid.NewGuid().ToByteArray() });
        await _db.SaveChangesAsync();
        return s;
    }

    private async Task<(string? Paragrafo, string Payload)> LeggiAsync(int sezione)
    {
        var blocchi = await _db.ContentBlocks.AsNoTracking().Where(b => b.SectionId == sezione).OrderBy(b => b.Order).ToListAsync();
        return (blocchi.SingleOrDefault(b => b.Format == BlockFormat.Prose)?.Body, blocchi.First().BodyJson!);
    }

    /// <summary>
    /// AoR: la nota di un volume diventa una riga col nome del volume; la classe scritta a mano resta, e un volume
    /// che aveva solo la nota esce dal JSON.
    /// </summary>
    [Fact]
    public async Task Le_note_degli_spazi_aerei_diventano_un_paragrafo_e_la_classe_resta()
    {
        var payload = JsonSerializer.Serialize(new AorExtraShapes
        {
            Callsigns = new() { "LIRQ_APP" },
            AirspaceEdits = new()
            {
                ["CTR|FIRENZE CTR|GND|FL085"] = new AorAirspaceEdit { Class = "D", Note = "Attivo H24" },
                ["TMA|FIRENZE TMA 1|1500|FL195"] = new AorAirspaceEdit { Note = "Solo con APP aperto" },
                ["TMA|FIRENZE TMA 2|FL105|FL195"] = new AorAirspaceEdit { Class = "C" },
            },
        });
        var s = await SezioneAsync("aor", payload);

        Assert.Equal(1, await _manutenzione.NoteDelleAreeNeiBlocchiAsync());

        var (paragrafo, json) = await LeggiAsync(s.Id);
        Assert.Equal("- FIRENZE CTR: Attivo H24\n- FIRENZE TMA 1: Solo con APP aperto", paragrafo);
        var riletto = JsonSerializer.Deserialize<AorExtraShapes>(json)!;
        Assert.Equal(new[] { "LIRQ_APP" }, riletto.Callsigns);
        Assert.Equal(new[] { "CTR|FIRENZE CTR|GND|FL085", "TMA|FIRENZE TMA 2|FL105|FL195" }, riletto.AirspaceEdits.Keys.OrderBy(k => k));
        Assert.All(riletto.AirspaceEdits.Values, e => Assert.Null(e.Note));
        Assert.Equal("D", riletto.AirspaceEdits["CTR|FIRENZE CTR|GND|FL085"].Class);

        // La seconda volta non c'è più niente: un payload con la sola classe non si riscrive a ogni avvio.
        Assert.Equal(0, await _manutenzione.NoteDelleAreeNeiBlocchiAsync());
    }

    /// <summary>Aree di lavoro (e BOAT, stessa forma): una riga per area col nome dell'area, e il JSON senza note.</summary>
    [Fact]
    public async Task Le_note_delle_aree_di_lavoro_diventano_un_paragrafo_col_nome_dell_area()
    {
        _db.SpecialAreas.Add(new SpecialArea { IvaoId = "1029", Name = "LI R14A - S.Severa" });
        await _db.SaveChangesAsync();
        var selezione = new RegulatedSelection { OwnAuto = false, OwnIds = new() { "1029", "2044" } };
        var payload = MilRegulatedPayload.Scrivi(selezione, new Dictionary<string, MilActivity>(),
            new Dictionary<string, string> { ["1029"] = "Coordinare con il poligono", ["2044"] = "Solo VFR" });
        var s = await SezioneAsync(SectionKeys.Regulated, payload);

        Assert.Equal(1, await _manutenzione.NoteDelleAreeNeiBlocchiAsync());

        var (paragrafo, json) = await LeggiAsync(s.Id);
        Assert.Equal("- LI R14A - S.Severa: Coordinare con il poligono\n- 2044: Solo VFR", paragrafo);
        Assert.Empty(MilRegulatedPayload.LeggiNote(json));
        Assert.Contains("1029", json);   // la selezione resta

        Assert.Equal(0, await _manutenzione.NoteDelleAreeNeiBlocchiAsync());
    }
}
