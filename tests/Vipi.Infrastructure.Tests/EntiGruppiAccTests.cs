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
/// Gli enti anche per gli APP della vIPI ACC (S55, committente, 29 settembre 2026): ogni gruppo APP ha il suo ente,
/// che vive nella vIPI dell'ACC e prende i membri come posizioni. Così un APP di ACC ha la stessa identità di un APP
/// con la vIPI propria — segue le rinomine e «Sostituisci con…», compare nella pagina degli enti, e la vista live lo
/// ritrova per chiave del gruppo anche quando i membri cambiano.
/// </summary>
public class EntiGruppiAccTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private Acc _lirr = default!;
    private int _pubblicata, _bozza;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(_lirr);
        var vipi = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        _db.Documents.Add(vipi);
        await _db.SaveChangesAsync();
        _db.Sectors.Add(new Sector { Acc = _lirr, Callsign = "LIRR_CTR", Name = "Roma", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true, DocumentId = vipi.Id, IsPrimary = true });
        var pubblicata = new DocumentVersion { DocumentId = vipi.Id, VersionNumber = 1, AiracCycle = "2610", Status = DocumentStatus.Published };
        var bozza = new DocumentVersion { DocumentId = vipi.Id, VersionNumber = 2, AiracCycle = "2610", Status = DocumentStatus.Draft };
        _db.DocumentVersions.AddRange(pubblicata, bozza);
        await _db.SaveChangesAsync();
        (_pubblicata, _bozza) = (pubblicata.Id, bozza.Id);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task GruppoAsync(int versione, string titolo, string chiave, params string[] membri)
    {
        var s = new DocumentSection { DocumentVersionId = versione, Title = titolo, Order = 1, Depth = 0, SectionKey = "appgroup" };
        _db.DocumentSections.Add(s);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = versione, SectionId = s.Id, Order = 0, Format = BlockFormat.Table,
            BodyJson = JsonSerializer.Serialize(new AccBlockMeta { Key = chiave, Kind = AccBlockKind.AppGroup, MemberCallsigns = membri.ToList() }),
        });
        await _db.SaveChangesAsync();
    }

    private Task<int> AllineaAsync() => new EfAtcUnitRepository(_db).AllineaGruppiAccAsync();

    [Fact]
    public async Task Ogni_gruppo_app_con_membri_ha_il_suo_ente_e_rifarlo_non_cambia_niente()
    {
        await GruppoAsync(_pubblicata, "Napoli", "grp:napoli", "LIRN_US0_APP", "LIRN_E_APP");
        await GruppoAsync(_pubblicata, "Nuovo gruppo APP", "grp:vuoto");   // appena aggiunto, ancora senza membri

        Assert.Equal(1, await AllineaAsync());
        Assert.Equal(0, await AllineaAsync());   // idempotente: gira a ogni avvio

        var ente = (await new EfAtcUnitRepository(_db).FindAsync("LIRN_E_APP"))!;
        Assert.Equal(("LIRN_US0_APP", "Napoli", "LIRR", AtcUnitMode.InAccVipi, (int?)null, "grp:napoli"),
            (ente.Code, ente.Name, ente.AccCode, ente.Mode, ente.DocumentId, ente.GroupKey));
        Assert.Equal(new[] { "LIRN_US0_APP", "LIRN_E_APP" }, ente.Positions);
    }

    [Fact]
    public async Task I_membri_della_bozza_diventano_le_posizioni_e_il_codice_resta()
    {
        await GruppoAsync(_pubblicata, "Napoli", "grp:napoli", "LIRN_US0_APP");
        await AllineaAsync();
        // Nella bozza il gruppo cambia nome e membri: IVAO ha rimesso l'APP sotto un altro nominativo.
        await GruppoAsync(_bozza, "Napoli Radar", "grp:napoli", "LIRR_US0_APP", "LIRN_E_APP");

        Assert.Equal(0, await AllineaAsync());

        var ente = (await new EfAtcUnitRepository(_db).FindAsync("LIRN_US0_APP"))!;   // per codice: non cambia mai
        Assert.Equal("Napoli Radar", ente.Name);
        Assert.Equal(new[] { "LIRR_US0_APP", "LIRN_E_APP" }, ente.Positions);
    }

    [Fact]
    public async Task Un_membro_che_e_gia_di_un_ente_resta_suo_e_uno_spostamento_in_corso_non_si_tocca()
    {
        // Pratica ha la sua vIPI APP e la posizione LIRE_TWR; un gruppo che la elenca non gliela porta via.
        var repo = new EfAtcUnitRepository(_db);
        var pratica = new AtcUnit { Code = "LIRE_APP", Name = "Pratica", AccId = _lirr.Id };
        pratica.Positions.Add(new AtcUnitPosition { Callsign = "LIRE_TWR", Order = 0 });
        // Palermo è copiata nella bozza (S52): il gruppo porta il suo UnitId, e l'ente ha ancora la sua vIPI APP.
        var palermo = new AtcUnit { Code = "LICJ_APP", Name = "Palermo Radar", AccId = _lirr.Id };
        palermo.Positions.Add(new AtcUnitPosition { Callsign = "LICJ_APP", Order = 0 });
        _db.AtcUnits.AddRange(pratica, palermo);
        await _db.SaveChangesAsync();
        await GruppoAsync(_pubblicata, "Latina", "grp:latina", "LIRE_TWR", "LIRL_APP");
        var s = new DocumentSection { DocumentVersionId = _bozza, Title = "Palermo Radar", Order = 2, Depth = 0, SectionKey = "appgroup" };
        _db.DocumentSections.Add(s);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = _bozza, SectionId = s.Id, Order = 0, Format = BlockFormat.Table,
            BodyJson = JsonSerializer.Serialize(new AccBlockMeta { Key = "grp:pa", Kind = AccBlockKind.AppGroup, MemberCallsigns = { "LICJ_APP", "LICP_APP" }, UnitId = palermo.Id }),
        });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await AllineaAsync());

        Assert.Equal(new[] { "LIRE_TWR" }, (await repo.FindAsync("LIRE_APP"))!.Positions);
        Assert.Equal(new[] { "LIRL_APP" }, (await repo.FindAsync("LIRL_APP"))!.Positions);
        var pa = (await repo.FindAsync("LICJ_APP"))!;
        Assert.Equal((AtcUnitMode.OwnDocument, "grp:pa"), (pa.Mode, pa.GroupKey));   // legato al gruppo, ma ancora suo
        Assert.Equal(new[] { "LICJ_APP" }, pa.Positions);
    }
}
