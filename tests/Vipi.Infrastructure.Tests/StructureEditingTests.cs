using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Authoring struttura da zero (DB vuoto): crea ACC → settori (entità unificata) → frequenze,
/// e verifica che <see cref="EfStationDirectory"/> esponga la ACC appena creata.
/// </summary>
public class StructureEditingTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfStructureEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfStructureEditingRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Builds_Full_Acc_From_Scratch()
    {
        var accId = await _repo.CreateAccAsync("LIMM", "Milano ACC", "LI");
        Assert.True(accId > 0);
        Assert.True(await _repo.AccExistsAsync("LIMM"));

        var secId = await _repo.AddSectorAsync("LIMM", "LIMM_NW_CTR", SectorType.Ctr, SectorKind.Acc,
            "Milano Radar NW", "128.800", 10, null, null, null);
        var childId = await _repo.AddSectorAsync("LIMM", "LIMM_N_CTR", SectorType.Ctr, SectorKind.Acc,
            "Milano Radar N", "133.250", 11, null, secId, null);
        await _repo.SetSectorFrequencyAsync("LIMM", secId, "128.805");   // frequenza = attributo del settore

        var data = await _repo.LoadAsync("LIMM");
        Assert.NotNull(data);
        Assert.Equal(2, data!.Sectors.Count);
        Assert.Contains(data.Sectors, s => s.Id == childId && s.ParentSectorId == secId);
        Assert.Equal("128.805", data.Sectors.Single(s => s.Id == secId).DefaultFrequency);

        // La directory di navigazione vede la ACC creata.
        var accs = new EfStationDirectory(_db).ListAccs();
        Assert.Contains(accs, a => a.Code == "LIMM" && a.Name == "Milano ACC");
    }

    [Fact]
    public async Task Duplicate_Callsign_Is_Rejected()
    {
        await _repo.CreateAccAsync("LIMM", "Milano ACC", "LI");
        await _repo.AddSectorAsync("LIMM", "LIMM_NW_CTR", SectorType.Ctr, SectorKind.Acc, "NW", null, 10, null, null, null);
        Assert.True(await _repo.CallsignExistsAsync("LIMM_NW_CTR"));
    }

    [Fact]
    public async Task AutoAssign_Creates_Only_Known_Acc_And_Skips_Existing()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");   // già assegnato

        var candidates = new List<(string AccCode, string Icao, string Name)>
        {
            ("LIRR", "LIRF", "Roma Fiumicino"),  // skip: ICAO già presente
            ("LIRR", "LIRA", "Roma Ciampino"),   // crea
            ("LIMM", "LIMC", "Milano Malpensa"), // skip: ACC inesistente
        };

        var created = await _repo.AutoAssignAirportsAsync(candidates);

        Assert.Equal(new[] { "LIRA" }, created);
        var data = await _repo.LoadAsync("LIRR");
        Assert.Equal(2, data!.Airports.Count);
        Assert.Contains(data.Airports, a => a.Icao == "LIRA");
        Assert.False(await _repo.AirportIcaoExistsAsync("LIMC"));
    }

    private static List<(SectorType, string, string?)> RomePositions() => new()
    {
        (SectorType.Twr, "LIRF_TWR", "118.700"),
        (SectorType.Gnd, "LIRF_GND", "121.700"),
        (SectorType.Del, "LIRF_DEL", "121.900"),
    };

    [Fact]
    public async Task EnsureSectors_Merge_Rebuild_Creates_Draft_Doc_With_Managed_Sections()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportProfileRepository(_db);

        var (created, found) = await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());
        Assert.True(found);
        Assert.Equal(3, created);   // ATIS non crea settore operativo

        // Catalogo settori (fonte delle frequenze del documento): ATIS + TWR.
        await new EfAirportSectorRepository(_db).ImportForAirportAsync("LIRF", new[]
        {
            new SourceAtcPosition("LIRF_ATIS", "135.975", "ATIS", null, null, null, null),
            new SourceAtcPosition("LIRF_TWR", "118.700", "TWR", null, null, null, null),
        });

        await profile.MergeFromSourceAsync("LIRF", 6000,
            new[] { ("16L", (int?)3902, (int?)160), ("16R", (int?)3900, (int?)160) });
        var docId = await profile.RebuildDocumentAsync("LIRF");
        Assert.True(docId > 0);

        // Settori: TWR primario, agganciato al documento; ATIS non è un settore.
        var data = await _repo.LoadAsync("LIRR");
        var twr = Assert.Single(data!.Sectors, s => s.Callsign == "LIRF_TWR");
        Assert.True(twr.IsPrimary);
        Assert.Equal(docId, twr.DocumentId);
        Assert.DoesNotContain(data.Sectors, s => s.Callsign == "LIRF_ATIS");

        // Documento generato in BOZZA (lo staff pubblica a mano) con le sezioni gestite.
        var doc = await _db.Documents.FirstAsync();
        Assert.Equal(DocumentStatus.Draft, doc.Status);
        var sectionTitles = await _db.DocumentSections.Select(s => s.Title).ToListAsync();
        Assert.Contains("Quote di transizione", sectionTitles);
        Assert.Contains("Frequenze", sectionTitles);
        Assert.Contains("Piste", sectionTitles);
        Assert.Contains("SID", sectionTitles);

        // ATIS (dal catalogo) nella tabella Frequenze; TA nella prose; piste con lunghezza.
        Assert.Contains(await _db.ContentBlocks.ToListAsync(), b => b.BodyJson != null && b.BodyJson.Contains("135.975"));
        Assert.Contains(await _db.ContentBlocks.ToListAsync(), b => b.Body != null && b.Body.Contains("6000 ft"));
        var prof = await profile.LoadAsync("LIRF");
        Assert.Equal(2, prof!.Runways.Count);
        Assert.Equal(3902, prof.Runways.First(r => r.Ident == "16L").LengthM);
        // Tabella TL di default: 4 fasce QNH, TL = TA(6000) + offset arrotondato al FL superiore.
        Assert.Equal(4, prof.TransitionLevels.Count);
        Assert.Equal("FL85", prof.TransitionLevels.Single(t => t.QnhTo == 976).Level);      // < 977 → +2500
        Assert.Equal("FL80", prof.TransitionLevels.Single(t => t.QnhFrom == 977).Level);    // 977–994 → +2000
        Assert.Equal("FL75", prof.TransitionLevels.Single(t => t.QnhFrom == 995).Level);    // 995–1012 → +1500
        Assert.Equal("FL70", prof.TransitionLevels.Single(t => t.QnhFrom == 1013).Level);   // ≥ 1013 → +1000
    }

    [Fact]
    public async Task Reimport_Overwrites_Ivao_Fields_But_Preserves_Editorial()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportProfileRepository(_db);
        await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());

        await profile.MergeFromSourceAsync("LIRF", 6000, new[] { ("16L", (int?)3902, (int?)160) });

        // Lo staff compila le colonne editoriali della pista.
        await profile.SaveRunwaysAsync("LIRF", new[]
        {
            new Vipi.Application.Content.RunwayRow(0, "16L", 3902, 160, "3902", "3902", "ILS CAT III", "Sx", "No"),
        });

        // Re-import con lunghezza cambiata: sovrascrive Length, preserva APP/Patterns/Circling.
        await profile.MergeFromSourceAsync("LIRF", 6000, new[] { ("16L", (int?)3950, (int?)160) });

        var data = await profile.LoadAsync("LIRF");
        var rw = Assert.Single(data!.Runways);
        Assert.Equal(3950, rw.LengthM);            // campo IVAO sovrascritto
        Assert.Equal("ILS CAT III", rw.AppProcedures); // editoriale preservato
        Assert.Equal("Sx", rw.Patterns);
        Assert.Equal("No", rw.Circling);
    }

    [Fact]
    public async Task Rebuild_Renders_Rules_And_Links_And_Preserves_Manual_Sections()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportProfileRepository(_db);
        await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());
        await profile.MergeFromSourceAsync("LIRF", 6000, new[] { ("16L", (int?)3902, (int?)160) });
        await profile.RebuildDocumentAsync("LIRF");

        // Una regola pista + un link a una frequenza esistente nel DB (entità Frequency di un settore APP).
        await profile.SaveRunwayRulesAsync("LIRF", new[]
        {
            new Vipi.Application.Content.RunwayRuleRow(0, "16R", "16L", "Sud", 5, null, RunwaySurface.Any, "vento da sud"),
        });
        var appSec = await _repo.AddSectorAsync("LIRR", "LIRF_APP", SectorType.App, SectorKind.Acc, "Roma APP", "119.200", 5, null, null, null);
        await profile.SaveFrequencyLinksAsync("LIRF", new[] { appSec });   // link al SETTORE (DefaultFrequency)

        // Aggiungo a mano una sezione extra al documento: il rebuild deve preservarla.
        var ver = await _db.DocumentVersions.Include(v => v.Sections).FirstAsync();
        _db.DocumentSections.Add(new Vipi.Domain.Entities.DocumentSection
        {
            DocumentVersionId = ver.Id, Title = "Note locali", Order = 99, Depth = 0,
            SectionKey = "custom", RowVersion = System.Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        await profile.RebuildDocumentAsync("LIRF");

        var titles = await _db.DocumentSections.Select(s => s.Title).ToListAsync();
        Assert.Contains("Regole piste", titles);
        Assert.Contains("Note locali", titles);                       // sezione manuale preservata
        Assert.Single(titles, t => t == "Frequenze");                 // non duplicata
        var freqBlock = await _db.ContentBlocks.FirstAsync(b => b.BodyJson != null && b.BodyJson.Contains("LIRF_APP"));
        Assert.Contains("119.200", freqBlock.BodyJson!);              // valore del link risolto
    }

    [Fact]
    public async Task Rebuild_Emits_Extra_Sections_Into_Document_And_Regenerates_Them()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportProfileRepository(_db);
        await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());
        await profile.MergeFromSourceAsync("LIRF", 6000, new[] { ("16L", (int?)3902, (int?)160) });
        await profile.RebuildDocumentAsync("LIRF");

        // Salvo due sezioni editoriali libere + rebuild: entrano nel documento come sezioni keyed "airportextra".
        await profile.SaveExtraSectionsAsync("LIRF", new[]
        {
            new Vipi.Application.Content.ExtraSectionRow(0, "Hot spot", "Attenzione al **raccordo B**."),
            new Vipi.Application.Content.ExtraSectionRow(0, "Rumore", "Procedure antirumore notturne."),
        });
        await profile.RebuildDocumentAsync("LIRF");

        var extras = await _db.DocumentSections.Where(s => s.SectionKey == "airportextra").OrderBy(s => s.Order).ToListAsync();
        Assert.Equal(new[] { "Hot spot", "Rumore" }, extras.Select(s => s.Title));
        var body = await _db.ContentBlocks.FirstAsync(b => b.SectionId == extras[0].Id);
        Assert.Contains("raccordo B", body.Body!);

        // Rinomino/riduco a una sola sezione + rebuild: le sezioni extra sono rigenerate (nessun duplicato/orfano).
        await profile.SaveExtraSectionsAsync("LIRF", new[]
        {
            new Vipi.Application.Content.ExtraSectionRow(0, "Solo questa", "Testo unico."),
        });
        await profile.RebuildDocumentAsync("LIRF");

        var after = await _db.DocumentSections.Where(s => s.SectionKey == "airportextra").ToListAsync();
        Assert.Equal("Solo questa", Assert.Single(after).Title);
        // Le sezioni gestite restano singole (rebuild idempotente).
        Assert.Single(await _db.DocumentSections.Where(s => s.Title == "Frequenze").ToListAsync());
    }

    [Fact]
    public async Task Default_Transition_Levels_Follow_Ta()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportProfileRepository(_db);

        // TA ignota: la tabella di default mostra la formula TA + offset.
        await profile.MergeFromSourceAsync("LIRF", null, Array.Empty<(string, int?, int?)>());
        var noTa = await profile.LoadAsync("LIRF");
        Assert.Equal(4, noTa!.TransitionLevels.Count);
        Assert.Equal("TA + 2500 ft", noTa.TransitionLevels.Single(t => t.QnhTo == 976).Level);

        // Salvando la TA, le righe di default si ricalcolano in FL.
        await profile.SetTransitionAltitudeAsync("LIRF", 4000);
        var withTa = await profile.LoadAsync("LIRF");
        Assert.Equal("FL65", withTa!.TransitionLevels.Single(t => t.QnhTo == 976).Level);    // 4000+2500
        Assert.Equal("FL50", withTa.TransitionLevels.Single(t => t.QnhFrom == 1013).Level);  // 4000+1000

        // Una fascia personalizzata (boundary diversa) non viene ricalcolata al cambio TA.
        await profile.SaveTransitionLevelsAsync("LIRF", new[]
        {
            new Vipi.Application.Content.TlRow(0, null, 950, "FL999"),   // fascia custom
        });
        await profile.SetTransitionAltitudeAsync("LIRF", 8000);
        var custom = await profile.LoadAsync("LIRF");
        Assert.Equal("FL999", Assert.Single(custom!.TransitionLevels).Level);
    }

    [Fact]
    public async Task Airport_Must_Keep_At_Least_One_Tower()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        var apId = await _repo.CreateAirportAsync("LIRR", "LIRN", "Napoli");

        // Aeroporto senza settori → segnalato come "senza torre".
        var before = await _repo.ListAllAirportsAsync();
        Assert.False(Assert.Single(before, a => a.Icao == "LIRN").HasTower);

        var twrId = await _repo.AddSectorAsync("LIRR", "LIRN_TWR", SectorType.Twr, SectorKind.Airport,
            "Napoli Torre", "118.700", 10, null, null, apId);

        var after = await _repo.ListAllAirportsAsync();
        Assert.True(Assert.Single(after, a => a.Icao == "LIRN").HasTower);

        // L'unica torre non si può eliminare (invariante "ogni aeroporto ha sempre una torre").
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.DeleteSectorAsync("LIRR", twrId));

        // Con una seconda torre (I_TWR) la prima diventa eliminabile.
        await _repo.AddSectorAsync("LIRR", "LIRN_I_TWR", SectorType.ITwr, SectorKind.Airport,
            "Napoli Informazioni", "118.700", 10, null, null, apId);
        await _repo.DeleteSectorAsync("LIRR", twrId);

        var data = await _repo.LoadAsync("LIRR");
        Assert.DoesNotContain(data!.Sectors, s => s.Id == twrId);
        Assert.Contains(data.Sectors, s => s.Type == SectorType.ITwr);
    }

    [Fact]
    public async Task Airport_Without_Sectors_Is_Hidden_By_Default_And_Admin_Can_Hide_With_Sectors()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        var apId = await _repo.CreateAirportAsync("LIRR", "LIRN", "Napoli");

        // Senza settori → non pubblico di default (anche se non nascosto a mano).
        var adminNoSec = Assert.Single(await _repo.ListAllAirportsAsync(), a => a.Icao == "LIRN");
        Assert.False(adminNoSec.IsHidden);
        Assert.False(adminNoSec.IsPublic);
        Assert.False(Assert.Single((await _repo.LoadAsync("LIRR"))!.Airports).IsPublic);

        // Con un settore → diventa pubblico.
        await _repo.AddSectorAsync("LIRR", "LIRN_TWR", SectorType.Twr, SectorKind.Airport,
            "Napoli Torre", "118.700", 10, null, null, apId);
        Assert.True(Assert.Single(await _repo.ListAllAirportsAsync(), a => a.Icao == "LIRN").IsPublic);
        Assert.True(Assert.Single((await _repo.LoadAsync("LIRR"))!.Airports).IsPublic);

        // L'admin lo nasconde → non più pubblico; mostrandolo torna pubblico.
        await _repo.SetAirportHiddenAsync("LIRR", apId, true);
        var hidden = Assert.Single(await _repo.ListAllAirportsAsync(), a => a.Icao == "LIRN");
        Assert.True(hidden.IsHidden);
        Assert.False(hidden.IsPublic);
        Assert.False(Assert.Single((await _repo.LoadAsync("LIRR"))!.Airports).IsPublic);

        await _repo.SetAirportHiddenAsync("LIRR", apId, false);
        Assert.True(Assert.Single(await _repo.ListAllAirportsAsync(), a => a.Icao == "LIRN").IsPublic);
    }

    [Fact]
    public async Task Hidden_Airport_Document_Is_Not_Served_Publicly()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        var apId = await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportProfileRepository(_db);
        await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());
        await profile.MergeFromSourceAsync("LIRF", 6000, new[] { ("16L", (int?)3902, (int?)160) });
        await profile.RebuildDocumentAsync("LIRF");

        var content = new EfContentRepository(_db, TestReleaseTargets.ReleaseRepo(_db));
        // Alla generazione il doc nasce in BOZZA: non ancora servito al pubblico finché lo staff non pubblica.
        Assert.Null(await content.LoadAirportVipiAsync("LIRF"));
        await PublishAirportDocAsync("LIRF");
        Assert.NotNull(await content.LoadAirportVipiAsync("LIRF"));   // visibile: documento servito

        await _repo.SetAirportHiddenAsync("LIRR", apId, true);
        Assert.Null(await content.LoadAirportVipiAsync("LIRF"));      // nascosto: pagina pubblica inaccessibile

        await _repo.SetAirportHiddenAsync("LIRR", apId, false);
        Assert.NotNull(await content.LoadAirportVipiAsync("LIRF"));   // di nuovo visibile
    }

    [Fact]
    public async Task Deleting_Sector_Removes_It()
    {
        await _repo.CreateAccAsync("LIMM", "Milano ACC", "LI");
        var secId = await _repo.AddSectorAsync("LIMM", "LIMM_NW_CTR", SectorType.Ctr, SectorKind.Acc, "NW", "128.800", 10, null, null, null);

        await _repo.DeleteSectorAsync("LIMM", secId);

        var data = await _repo.LoadAsync("LIMM");
        Assert.Empty(data!.Sectors);
    }

    // Simula la pubblicazione manuale dello staff: porta doc + versione corrente a Published.
    private async Task PublishAirportDocAsync(string icao)
    {
        var doc = await _db.Documents.Include(d => d.Versions)
            .FirstAsync(d => d.Type == DocumentType.Vipi && d.Title.Contains(icao));
        doc.Status = DocumentStatus.Published;
        foreach (var v in doc.Versions) v.Status = DocumentStatus.Published;
        await _db.SaveChangesAsync();
    }
}
