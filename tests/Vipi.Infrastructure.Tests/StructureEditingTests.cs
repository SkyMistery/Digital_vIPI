using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
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
    public async Task SetSectorFrequency_Rejects_Projected_Sector()
    {
        // Un settore PROIETTATO ha la frequenza di sorgente: editarla darebbe l'illusione di una modifica che il
        // prossimo SyncFromCatalogsAsync cancella in silenzio. Il repo la rifiuta (fonte unica = catalogo).
        var accId = await _repo.CreateAccAsync("LIMM", "Milano ACC", "LI");
        var projected = new Vipi.Domain.Entities.Sector
        {
            AccId = accId, Callsign = "LIMM_NW_CTR", Name = "Milano Radar NW",
            Type = SectorType.Ctr, Kind = SectorKind.Acc, DefaultFrequency = "128.800",
            IsProjected = true, IsActive = true,
        };
        _db.Sectors.Add(projected);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.SetSectorFrequencyAsync("LIMM", projected.Id, "128.805"));

        // Frequenza invariata: l'edit è stato respinto, non applicato a metà.
        Assert.Equal("128.800", (await _db.Sectors.AsNoTracking().FirstAsync(s => s.Id == projected.Id)).DefaultFrequency);
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
    public async Task EnsureSectors_Merge_Ensure_Creates_Draft_Doc_With_Catalog_Sections()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));

        var (created, found) = await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());
        Assert.True(found);
        Assert.Equal(3, created);   // ATIS non crea settore operativo

        // Catalogo settori (fonte delle frequenze derivate): ATIS + TWR.
        await new EfAirportSectorRepository(_db).ImportForAirportAsync("LIRF", new[]
        {
            new SourceAtcPosition("LIRF_ATIS", "135.975", "ATIS", null, null, null, null),
            new SourceAtcPosition("LIRF_TWR", "118.700", "TWR", null, null, null, null),
        });

        await profile.MergeFromSourceAsync("LIRF", 6000,
            new[] { ("16L", (int?)3902, (int?)160), ("16R", (int?)3900, (int?)160) });
        var docId = await profile.EnsureDocumentAsync("LIRF");
        Assert.True(docId > 0);

        // Settori: TWR primario, agganciato al documento; ATIS non è un settore.
        var data = await _repo.LoadAsync("LIRR");
        var twr = Assert.Single(data!.Sectors, s => s.Callsign == "LIRF_TWR");
        Assert.True(twr.IsPrimary);
        Assert.Equal(docId, twr.DocumentId);
        Assert.DoesNotContain(data.Sectors, s => s.Callsign == "LIRF_ATIS");

        // Documento in BOZZA (lo staff pubblica a mano) con le sezioni del CATALOGO, nel loro ordine.
        var doc = await _db.Documents.FirstAsync();
        Assert.Equal(DocumentStatus.Draft, doc.Status);
        var sections = await _db.DocumentSections.OrderBy(s => s.Order).ToListAsync();
        Assert.Equal(
            SectionCatalog.For(SectionProfile.Airport).OrderBy(d => d.Order).Select(d => d.Key),
            sections.Select(s => s.SectionKey));
        Assert.Equal(
            SectionCatalog.For(SectionProfile.Airport).OrderBy(d => d.Order).Select(d => d.Title),
            sections.Select(s => s.Title));

        // ⚠️ Nessuna sezione porta blocchi: il corpo delle fisse lo produce la pagina, derivandolo dalle
        // tabelle del profilo (carta 2026-08-26 §2). Prima erano tabelle Markdown COTTE qui dentro, ed è per
        // questo che l'ordine e il «nascondi» non sopravvivevano a un rebuild.
        Assert.Empty(await _db.ContentBlocks.ToListAsync());

        // Meteo e SID nascono Live, il resto Frozen: si congela alla pubblicazione.
        Assert.Equal(RenderMode.Live, sections.Single(s => s.SectionKey == "weather").RenderMode);
        Assert.Equal(RenderMode.Live, sections.Single(s => s.SectionKey == "sids").RenderMode);
        Assert.Equal(RenderMode.Frozen, sections.Single(s => s.SectionKey == "runways").RenderMode);
        Assert.Equal(RenderMode.Frozen, sections.Single(s => s.SectionKey == "frequencies").RenderMode);

        // I dati del profilo restano dove sono: sono loro la sorgente delle sezioni derivate.
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

    /// <summary>
    /// Le SID nascono Live, e una seconda <c>EnsureDocumentAsync</c> non rigenera niente — era il punto
    /// dolente del rebuild, quando le sezioni venivano distrutte e riscritte a ogni apertura dell'editor.
    /// <para>
    /// ⚠️ Questa prova passava da <c>GetSidsRenderModeAsync</c>/<c>SetSidsRenderModeAsync</c>, che erano gli
    /// UNICI suoi chiamanti in tutto il progetto: dal 26 agosto 2026 il Live/Frozen delle sezioni lo governa
    /// l'editor condiviso, e quei due metodi erano rimasti a leggere <c>CurrentVersionId</c> col significato
    /// sbagliato. Sono stati tolti; le due proprietà si guardano dove vivono davvero, sulla sezione.
    /// </para>
    /// </summary>
    [Fact]
    public async Task SidsRenderMode_Defaults_Live_And_Survives_A_Second_Ensure()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());
        await profile.MergeFromSourceAsync("LIRF", 6000, new[] { ("16L", (int?)3902, (int?)160) });

        await profile.EnsureDocumentAsync("LIRF");
        var sids = await _db.DocumentSections.SingleAsync(s => s.SectionKey == "sids");
        Assert.Equal(RenderMode.Live, sids.RenderMode);   // nascono Live: una SID si mostra sempre aggiornata

        // Lo staff congela la sezione SID (è ciò che fa il toggle dell'editor condiviso).
        sids.RenderMode = RenderMode.Frozen;
        await _db.SaveChangesAsync();

        // Una seconda Ensure non tocca niente: è idempotente, non rigenera.
        await profile.EnsureDocumentAsync("LIRF");
        var dopo = await _db.DocumentSections.SingleAsync(s => s.SectionKey == "sids");
        Assert.Equal(RenderMode.Frozen, dopo.RenderMode);
        Assert.Equal(sids.Id, dopo.Id);   // è la STESSA sezione, non una riscritta con lo stesso nome
    }

    [Fact]
    public async Task Reimport_Overwrites_Ivao_Fields_But_Preserves_Editorial()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
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

    /// <summary>
    /// L'Ensure è idempotente e NON tocca il documento che trova: né l'ordine deciso in editor, né le sezioni
    /// libere, né i loro blocchi. È la differenza col rebuild di prima, che cancellava e riscriveva.
    /// </summary>
    [Fact]
    public async Task Ensure_Is_Idempotent_And_Leaves_The_Existing_Document_Alone()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());
        await profile.MergeFromSourceAsync("LIRF", 6000, new[] { ("16L", (int?)3902, (int?)160) });
        var docId = await profile.EnsureDocumentAsync("LIRF");

        // Lo staff riordina (Frequenze in testa) e aggiunge una sezione libera con del testo.
        var ver = await _db.DocumentVersions.Include(v => v.Sections).FirstAsync();
        ver.Sections.Single(s => s.SectionKey == "frequencies").Order = 0;
        var libera = new Vipi.Domain.Entities.DocumentSection
        {
            DocumentVersionId = ver.Id, Title = "Note locali", Order = 99, Depth = 0,
            SectionKey = SectionKeys.NewCustom(), RowVersion = System.Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(libera);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new Vipi.Domain.Entities.ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = libera.Id, Order = 1, Format = BlockFormat.Prose,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            Body = "Attenzione al **raccordo B**.", RowVersion = System.Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        Assert.Equal(docId, await profile.EnsureDocumentAsync("LIRF"));   // stesso documento, nessun gemello

        var sezioni = await _db.DocumentSections.OrderBy(s => s.Order).ToListAsync();
        Assert.Equal("frequencies", sezioni[0].SectionKey);              // l'ordine deciso in editor tiene
        Assert.Equal("Note locali", sezioni[^1].Title);                  // la sezione libera è ancora lì
        var blocco = Assert.Single(await _db.ContentBlocks.ToListAsync());
        Assert.Contains("raccordo B", blocco.Body!);                     // e il suo testo pure
        Assert.Single(await _db.DocumentSections.Where(s => s.SectionKey == "frequencies").ToListAsync());
    }

    [Fact]
    public async Task Default_Transition_Levels_Follow_Ta()
    {
        await _repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await _repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));

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
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        await _repo.EnsureAirportSectorsAsync("LIRF", RomePositions());
        await profile.MergeFromSourceAsync("LIRF", 6000, new[] { ("16L", (int?)3902, (int?)160) });
        await profile.EnsureDocumentAsync("LIRF");

        var releases = TestReleaseTargets.ReleaseRepo(_db);
        var content = new EfContentRepository(_db, releases);
        // Alla generazione il doc nasce in BOZZA: non ancora servito al pubblico finché non c'è una release effettiva.
        Assert.Null(await content.LoadAirportVipiAsync("LIRF"));

        // Doc 10 §S6b: visibilità pubblica = release effettiva. Pubblico la versione + creo una release in vigore ora.
        await PublishAirportDocAsync("LIRF");
        var snap = (await releases.SnapshotWorkingAsync(ReleaseTargetType.Airport, "LIRF", "2607"))!;
        await releases.SaveReleaseAsync(ReleaseTargetType.Airport, "LIRF", "2607", DateTime.UtcNow.AddMinutes(-1), snap, 1, null);
        Assert.NotNull(await content.LoadAirportVipiAsync("LIRF"));   // visibile: release effettiva servita

        await _repo.SetAirportHiddenAsync("LIRR", apId, true);
        Assert.Null(await content.LoadAirportVipiAsync("LIRF"));      // nascosto: pagina pubblica inaccessibile anche con release

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
