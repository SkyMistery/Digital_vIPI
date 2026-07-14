using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Round 20 — fonte unica: i Sector operativi sono una PROIEZIONE dei cataloghi (AccSector/AirportSector)
/// e la gerarchia di copertura vive per callsign (cross-ACC). Verifica sync + editing gerarchia.
/// </summary>
public class SectorProjectionTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorProjectionService _proj = default!;
    private EfHierarchyEditingService _hier = default!;

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? User { get; set; }
        public CurrentUser? Get() => User;
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        // Due ACC: Roma (LIRR) e Brindisi (LIBB), per i casi cross-ACC.
        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        var libb = new Acc { Code = "LIBB", Name = "Brindisi", CountryPrefix = "LI" };
        _db.Accs.AddRange(lirr, libb);

        // Settori ACC (catalogo): NE radice, TS sotto NE.
        _db.AccSectors.AddRange(
            new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR", Frequency = "128.800" },
            new AccSector { ComposePosition = "LIRR_TS_CTR", CenterId = "LIRR", Position = "CTR", Frequency = "132.700", ParentCallsign = "LIRR_NE_CTR" });

        // Aeroporto LIRP (Pisa) in Roma, con le sue posizioni (APP nodo interno; TWR/GND/DEL foglie operative).
        var lirp = new Airport { Icao = "LIRP", Name = "Pisa", Acc = lirr };
        _db.Airports.Add(lirp);
        _db.AirportSectors.AddRange(
            new AirportSector { ComposePosition = "LIRP_APP", AirportIcao = "LIRP", AccCode = "LIRR", Position = "APP", Frequency = "124.500", ParentCallsign = "LIRR_NE_CTR" },
            new AirportSector { ComposePosition = "LIRP_TWR", AirportIcao = "LIRP", AccCode = "LIRR", Position = "TWR", Frequency = "118.300" },
            new AirportSector { ComposePosition = "LIRP_GND", AirportIcao = "LIRP", AccCode = "LIRR", Position = "GND", Frequency = "121.700" });

        // Aeroporto di Brindisi (LIBR) per il caso cross-ACC.
        var libr = new Airport { Icao = "LIBR", Name = "Brindisi", Acc = libb };
        _db.Airports.Add(libr);

        await _db.SaveChangesAsync();

        var provider = new FakeUser { User = new CurrentUser(1, "Admin", "LIRR", new[] { "IT-AOC" }) };
        var authz = new EditAuthorizationService(provider, new EfEditGrantRepository(_db),
            Options.Create(new AuthOptions()), Options.Create(new DivisionOptions()));
        _proj = new EfSectorProjectionService(_db);
        _hier = new EfHierarchyEditingService(_db, authz, _proj,
            Options.Create(new Vipi.Application.NeighboursOptions()), Options.Create(new DivisionOptions()));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Sync_Projects_Catalog_Sectors_With_Types_And_Parent()
    {
        await _proj.SyncFromCatalogsAsync();

        var ne = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_NE_CTR");
        var ts = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_TS_CTR");
        var app = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRP_APP");
        var twr = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRP_TWR");

        Assert.Equal(SectorType.Ctr, ne.Type);
        Assert.Equal(SectorKind.Acc, ne.Kind);
        Assert.Equal(SectorKind.Airport, app.Kind);
        Assert.Equal(SectorType.App, app.Type);
        Assert.Equal(SectorType.Twr, twr.Type);
        Assert.True(app.IsProjected && ne.IsProjected);

        // Contenimento derivato dal ParentCallsign del catalogo.
        Assert.Equal(ne.Id, ts.ParentSectorId);
        Assert.Equal(ne.Id, app.ParentSectorId);
        Assert.Null(ne.ParentSectorId);   // radice
    }

    [Fact]
    public async Task Sync_Names_Sectors_From_AtcCallsign_Then_Composed_And_Harmonizes_Placeholders()
    {
        // Un AtcCallsign IVAO su un settore; gli altri senza → nome composto "{ICAO} {Tipo}".
        var ts = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR");
        ts.AtcCallsign = "Roma Radar";
        // Segnaposto pre-esistente: un Sector già proiettato col Name = callsign grezzo (residuo vecchia proiezione).
        var lirr = await _db.Accs.FirstAsync(a => a.Code == "LIRR");
        _db.Sectors.Add(new Sector { AccId = lirr.Id, Callsign = "LIRP_TWR", Name = "LIRP_TWR",
            Type = SectorType.Twr, Kind = SectorKind.Airport, IsProjected = true, IsActive = true });
        // Nome personalizzato dall'admin (≠ callsign) → NON deve essere toccato.
        _db.Sectors.Add(new Sector { AccId = lirr.Id, Callsign = "LIRP_GND", Name = "Pisa Ground custom",
            Type = SectorType.Gnd, Kind = SectorKind.Airport, IsProjected = true, IsActive = true });
        await _db.SaveChangesAsync();

        await _proj.SyncFromCatalogsAsync();

        Assert.Equal("Roma Radar", (await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_TS_CTR")).Name); // AtcCallsign vince
        Assert.Equal("LIRR Control", (await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_NE_CTR")).Name); // composto
        Assert.Equal("LIRP Approach", (await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRP_APP")).Name);   // composto
        Assert.Equal("LIRP Tower", (await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRP_TWR")).Name);      // segnaposto riarmonizzato
        Assert.Equal("Pisa Ground custom", (await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRP_GND")).Name); // custom preservato
    }

    [Fact]
    public async Task Sync_Preserves_Id_And_Editorial_Flags_On_Existing_Sector()
    {
        // Un Sector già presente (es. seed/manuale) con lo stesso callsign e un flag editoriale.
        var preset = new Sector { AccId = (await _db.Accs.FirstAsync(a => a.Code == "LIRR")).Id,
            Callsign = "LIRR_NE_CTR", Name = "Roma NE storico", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsPrimary = true };
        _db.Sectors.Add(preset);
        await _db.SaveChangesAsync();
        var presetId = preset.Id;

        await _proj.SyncFromCatalogsAsync();

        var after = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_NE_CTR");
        Assert.Equal(presetId, after.Id);     // stesso Id → FK documento intatte
        Assert.True(after.IsPrimary);          // flag editoriale preservato
        Assert.True(after.IsProjected);        // ora marcato come proiezione
    }

    [Fact]
    public async Task Sync_Deactivates_Projected_Sector_When_Hidden_In_Catalog()
    {
        await _proj.SyncFromCatalogsAsync();
        Assert.True((await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_TS_CTR")).IsActive);

        var ts = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR");
        ts.IsHidden = true;
        await _db.SaveChangesAsync();

        await _proj.SyncFromCatalogsAsync();
        var sector = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_TS_CTR");
        Assert.False(sector.IsActive);   // disattivato, non cancellato
    }

    [Fact]
    public async Task Sync_Clears_Editorial_Links_When_Projected_Sector_Becomes_Orphan()
    {
        await _proj.SyncFromCatalogsAsync();

        // Aggancio manualmente il settore proiettato TS a un documento (come farebbe la generazione doc).
        var doc = new Document { Type = DocumentType.Vipi, Title = "Doc TS", Language = Language.It,
            LastUpdatedAiracCycle = "2606" };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var ts = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_TS_CTR");
        ts.DocumentId = doc.Id; ts.IsPrimary = true; ts.FeaturedRank = 2;
        await _db.SaveChangesAsync();

        // Il callsign sparisce dal catalogo (rimosso dalla sorgente) → orfano.
        _db.AccSectors.Remove(await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR"));
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        var after = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_TS_CTR");
        Assert.False(after.IsActive);          // disattivato
        Assert.Null(after.DocumentId);         // legame editoriale reciso (niente FK dangling)
        Assert.False(after.IsPrimary);
        Assert.Null(after.FeaturedRank);
    }

    [Fact]
    public async Task Sync_Reparents_Child_To_Nearest_Visible_Ancestor_When_Middle_Hidden()
    {
        // Catena 3 livelli: NE (radice) → TS → TS1. Nascondendo TS (nodo di mezzo), TS1 deve risalire a NE (nonno).
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIRR_TS1_CTR", CenterId = "LIRR", Position = "CTR",
            Frequency = "133.100", ParentCallsign = "LIRR_TS_CTR" });
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        var ne = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_NE_CTR");
        var ts = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_TS_CTR");
        Assert.Equal(ts.Id, (await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_TS1_CTR")).ParentSectorId);

        (await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR")).IsHidden = true;
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        var ts1 = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_TS1_CTR");
        Assert.Equal(ne.Id, ts1.ParentSectorId);   // risalito al nonno NE, non più agganciato al TS disattivato
        Assert.False((await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_TS_CTR")).IsActive);
    }

    [Fact]
    public async Task Topology_Excludes_Deactivated_Hidden_Sector()
    {
        // Un settore nascosto (→ IsActive=false) deve sparire dalla topologia del documento ACC (AoR/coordinamenti/config).
        await _proj.SyncFromCatalogsAsync();
        (await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR")).IsHidden = true;
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        var lirrId = (await _db.Accs.FirstAsync(a => a.Code == "LIRR")).Id;
        var topo = await new Vipi.Infrastructure.Aor.TopologyBuilder(_db).BuildAsync(lirrId);

        Assert.DoesNotContain("LIRR_TS_CTR", topo.Sectors);   // nascosto → fuori dalla topologia
        Assert.Contains("LIRR_NE_CTR", topo.Sectors);         // attivo → resta
    }

    [Fact]
    public async Task Sync_Child_Becomes_Root_When_Root_Ancestor_Hidden()
    {
        await _proj.SyncFromCatalogsAsync();
        // Nascondendo NE (radice), i figli (TS, LIRP_APP) non hanno nonno visibile → diventano radici.
        (await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_NE_CTR")).IsHidden = true;
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        Assert.Null((await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRR_TS_CTR")).ParentSectorId);
        Assert.Null((await _db.Sectors.AsNoTracking().FirstAsync(s => s.Callsign == "LIRP_APP")).ParentSectorId);
    }

    [Fact]
    public async Task LoadTree_Includes_Acc_App_Airport_But_Not_Del_Gnd_Twr()
    {
        var tree = await _hier.LoadTreeAsync();

        Assert.Contains(tree, n => n.Kind == HierarchyNodeKind.Acc && n.Callsign == "LIRR_NE_CTR");
        Assert.Contains(tree, n => n.Kind == HierarchyNodeKind.App && n.Callsign == "LIRP_APP");
        Assert.Contains(tree, n => n.Kind == HierarchyNodeKind.Airport && n.Label.StartsWith("LIRP"));
        // TWR/GND non sono nodi della gerarchia.
        Assert.DoesNotContain(tree, n => n.Callsign == "LIRP_TWR" || n.Callsign == "LIRP_GND");
    }

    [Fact]
    public async Task SetParent_CrossAcc_Airport_Under_Foreign_Sector()
    {
        // Brindisi (ACC LIBB) sotto un settore di Roma (ACC LIRR): cross-ACC ammesso.
        var libr = await _db.Airports.FirstAsync(a => a.Icao == "LIBR");
        await _hier.SetParentAsync(HierarchyNodeKind.Airport, libr.Id, "LIRR_NE_CTR");

        var after = await _db.Airports.AsNoTracking().FirstAsync(a => a.Id == libr.Id);
        Assert.Equal("LIRR_NE_CTR", after.ParentCallsign);

        await _hier.SetParentAsync(HierarchyNodeKind.Airport, libr.Id, null);
        Assert.Null((await _db.Airports.AsNoTracking().FirstAsync(a => a.Id == libr.Id)).ParentCallsign);
    }

    [Fact]
    public async Task SetParent_Rejects_Unknown_Parent_And_Cycle()
    {
        var ts = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR");
        var ne = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_NE_CTR");

        // Padre inesistente → rifiutato.
        await Assert.ThrowsAsync<ValidationException>(
            () => _hier.SetParentAsync(HierarchyNodeKind.Acc, ts.Id, "NON_ESISTE_CTR"));

        // TS è già padre di NE? No: NE è padre di TS (da seed). Mettere NE sotto TS creerebbe un ciclo.
        await Assert.ThrowsAsync<ValidationException>(
            () => _hier.SetParentAsync(HierarchyNodeKind.Acc, ne.Id, "LIRR_TS_CTR"));
    }
}
