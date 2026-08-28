using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;
using Vipi.Domain;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Import aree speciali/regolamentate: gate della policy di import (categoria esclusa = congelamento, niente
/// fetch e soprattutto niente prune) e upsert/prune per-ACC sul repository reale.
/// </summary>
public class SpecialAreaImportTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAccAdminRepository _repo = default!;
    private EfImportPolicyStore _policy = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfAccAdminRepository(_db);
        _policy = new EfImportPolicyStore(_db);

        await _repo.ImportAsync(new[] { new SourceCenter("LIRR_CTR", "LIRR", "Roma Control", false, "124.000") });
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static SourceSpecialArea Area(string id, string name = "LI R14A", string? polygon = "[[41,12]]") =>
        new(id, "R", name, "descrizione", "Permanently active", 0, 5000, false, "LIRR", polygon);

    [Fact]
    public async Task Default_policy_imports_areas()
    {
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("1"), Area("2", "LI R14B") } } };

        var r = await new SpecialAreaImportUseCase(_repo, dir, _policy).RunAsync();

        Assert.Equal(2, r.Created);
        Assert.Equal(2, await _db.SpecialAreas.CountAsync());
        Assert.Equal(1, dir.Calls);
    }

    [Fact]
    public async Task Excluded_category_skips_fetch_and_keeps_existing_areas()
    {
        // Un'area già in archivio da un import precedente.
        await _repo.ImportSpecialAreasAsync(new[] { Area("1") });

        // La sorgente ora non la espone più: con la categoria attiva verrebbe potata.
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() } };
        await _policy.SaveAsync(new ImportPolicySnapshot(true, true, true, true, SpecialAreas: false), 1);

        var r = await new SpecialAreaImportUseCase(_repo, dir, _policy).RunAsync();

        Assert.Equal(0, dir.Calls);                                  // nessuna fetch
        Assert.Equal(SpecialAreaImportResult.Empty, r);
        Assert.Equal(1, await _db.SpecialAreas.CountAsync());        // e nessun prune: l'area resta
    }

    [Fact]
    public async Task Second_run_skips_the_detail_of_areas_with_a_fresh_shape()
    {
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("1"), Area("2", "LI R14B") } } };
        var sut = new SpecialAreaImportUseCase(_repo, dir, _policy);

        await sut.RunAsync();                    // primo giro: shape assenti → dettaglio per tutte
        Assert.Empty(dir.SkippedDetails);

        dir.SkippedDetails.Clear();
        await sut.RunAsync();                    // secondo giro: shape in archivio e fresche → dettaglio saltato

        Assert.Equal(new[] { "1", "2" }, dir.SkippedDetails.OrderBy(x => x));
        // La shape salvata sopravvive al giro senza dettaglio (l'upsert non azzera su null).
        Assert.All(await _db.SpecialAreas.AsNoTracking().ToListAsync(), s => Assert.Equal("[[41,12]]", s.RegionMapPolygon));
    }

    [Fact]
    public async Task Area_without_shape_is_not_skipped()
    {
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("1", polygon: null), Area("2", "LI R14B") } } };
        var sut = new SpecialAreaImportUseCase(_repo, dir, _policy);

        await sut.RunAsync();
        dir.SkippedDetails.Clear();
        await sut.RunAsync();

        // Solo la 2 ha una shape in archivio: la 1 va ri-chiesta finché non arriva.
        Assert.Equal(new[] { "2" }, dir.SkippedDetails);
    }

    [Fact]
    public async Task Area_listed_by_two_accs_belongs_to_both()
    {
        // Caso reale: la R49 «Zita» compare sia nell'elenco di LIRR sia in quello del militare LIZZ. Prima vinceva
        // l'ultimo ACC in ordine alfabetico e spariva dalle aree proprie dell'altro.
        await _repo.ImportAsync(new[] { new SourceCenter("LIZZ_CTR", "LIZZ", "Legion", true, "130.000") });
        var dir = new FakeAccDirectory
        {
            Areas =
            {
                ["LIRR"] = new() { Area("8870", "LI R49 - Zita") },
                ["LIZZ"] = new() { Area("8870", "LI R49 - Zita") with { CenterId = "LIZZ" } },
            },
        };

        await new SpecialAreaImportUseCase(_repo, dir, _policy).RunAsync();

        Assert.Equal(1, await _db.SpecialAreas.CountAsync());                 // una sola area…
        Assert.Equal(2, await _db.SpecialAreaCenters.CountAsync());           // …elencata da due enti
        var areas = new EfSpecialAreaRepository(_db);
        Assert.Contains(await areas.ListSpecialAreasByAccAsync("LIRR"), p => p.IvaoId == "8870");
        Assert.Contains(await areas.ListSpecialAreasByAccAsync("LIZZ"), p => p.IvaoId == "8870");
        Assert.Empty(await areas.ListSpecialAreasExcludingAccAsync("LIRR"));
    }

    [Fact]
    public async Task Prune_of_one_acc_leaves_the_area_to_the_other()
    {
        await _repo.ImportAsync(new[] { new SourceCenter("LIZZ_CTR", "LIZZ", "Legion", true, "130.000") });
        // Una chiamata per ACC, come fa il use-case: dentro un batch la stessa area si tratta una volta sola.
        await _repo.ImportSpecialAreasAsync(new[] { Area("8870") });
        await _repo.ImportSpecialAreasAsync(new[] { Area("8870") with { CenterId = "LIZZ" } });

        // LIRR non la elenca più; LIZZ sì.
        var removed = await _repo.PruneSpecialAreasNotInAsync("LIRR", Array.Empty<string>());

        Assert.Equal(1, removed.Removed);                           // un legame, non l'area
        Assert.Equal("8870", Assert.Single(removed.Gone).IvaoId);   // e il nome per la segnalazione ai documenti
        Assert.Equal(1, await _db.SpecialAreas.CountAsync());
        var areas = new EfSpecialAreaRepository(_db);
        Assert.Empty(await areas.ListSpecialAreasByAccAsync("LIRR"));
        Assert.Single(await areas.ListSpecialAreasByAccAsync("LIZZ"));

        // Quando anche l'ultimo ente la molla, l'area sparisce (e la diagnostica segnalerà i documenti che la citano).
        await _repo.PruneSpecialAreasNotInAsync("LIZZ", Array.Empty<string>());
        Assert.Equal(0, await _db.SpecialAreas.CountAsync());
    }

    [Fact]
    public async Task Periodic_run_skips_accs_with_areas_disabled()
    {
        await _repo.ImportAsync(new[] { new SourceCenter("LFMM_CTR", "LFMM", "Marseille", false, "128.100") });
        var lfmm = (await _repo.ListAccsAsync()).Single(a => a.Code == "LFMM");
        await _repo.SetSpecialAreasEnabledAsync(lfmm.Id, false);          // com'è per gli esteri dopo la riconciliazione

        var dir = new FakeAccDirectory
        {
            Areas = { ["LIRR"] = new() { Area("1") }, ["LFMM"] = new() { Area("2", "LF R 66") with { CenterId = "LFMM" } } },
        };
        await new SpecialAreaImportUseCase(_repo, dir, _policy).RunAsync();

        Assert.Equal(1, dir.Calls);                                        // solo LIRR: l'estero non si interroga
        Assert.Equal(1, await _db.SpecialAreas.CountAsync());
    }

    [Fact]
    public async Task Manual_per_acc_import_enables_the_acc()
    {
        await _repo.ImportAsync(new[] { new SourceCenter("LFMM_CTR", "LFMM", "Marseille", false, "128.100") });
        var id = (await _repo.ListAccsAsync()).Single(a => a.Code == "LFMM").Id;
        await _repo.SetSpecialAreasEnabledAsync(id, false);

        var dir = new FakeAccDirectory
        {
            Areas = { ["LFMM"] = new() { Area("2", "LF R 66") with { CenterId = "LFMM" } } },
        };
        var sut = new SpecialAreaImportUseCase(_repo, dir, _policy);

        // Il primo scarico ignora il flag: è l'atto con cui l'admin accende l'ente.
        var r = await sut.RunForAccAsync("LFMM");

        Assert.Equal(1, r.Created);
        Assert.Single(await _db.SpecialAreas.ToListAsync());
        Assert.False((await _repo.ListAccsAsync()).Single(a => a.Code == "LFMM").SpecialAreasEnabled);   // lo abilita il service
    }

    [Fact]
    public async Task Disabling_an_acc_frees_its_areas_but_keeps_the_shared_ones()
    {
        await _repo.ImportAsync(new[] { new SourceCenter("LFMM_CTR", "LFMM", "Marseille", false, "128.100") });
        await _repo.ImportSpecialAreasAsync(new[] { Area("solo-lfmm", "LF R 66") with { CenterId = "LFMM" } });
        await _repo.ImportSpecialAreasAsync(new[] { Area("condivisa") });                                  // LIRR
        await _repo.ImportSpecialAreasAsync(new[] { Area("condivisa") with { CenterId = "LFMM" } });       // e anche LFMM

        var id = (await _repo.ListAccsAsync()).Single(a => a.Code == "LFMM").Id;
        var freed = await _repo.SetSpecialAreasEnabledAsync(id, false);

        Assert.Equal(2, freed);                                            // due legami tolti a LFMM
        var left = await _db.SpecialAreas.Select(a => a.IvaoId).ToListAsync();
        Assert.Equal(new[] { "condivisa" }, left);                         // la sua resta: LIRR la elenca ancora
    }

    [Fact]
    public async Task Failed_acc_does_not_prune_its_areas()
    {
        await _repo.ImportSpecialAreasAsync(new[] { Area("1") });
        var dir = new FakeAccDirectory { Throw = { "LIRR" } };

        var r = await new SpecialAreaImportUseCase(_repo, dir, _policy).RunAsync();

        Assert.Equal("LIRR", Assert.Single(r.Failures).AccCode);
        Assert.Equal(1, await _db.SpecialAreas.CountAsync());   // fetch fallita ⇒ nessuna cancellazione
    }


    // ---- Che cosa finisce nella casella degli impatti ------------------------------------------------

    /// <summary>
    /// ⚠️ Il caso che ha imposto il confronto campo per campo: l'upsert riassegna TUTTI i campi a ogni giro e
    /// contava una «aggiornata» ogni volta. Segnalare su quel contatore avrebbe aperto una riga per ogni area
    /// e per ogni documento che la cita, ogni notte, senza che fosse successo niente.
    /// </summary>
    [Fact]
    public async Task Un_Import_Che_Non_Cambia_Niente_Non_Segnala_Niente()
    {
        var doc = await DocumentoCheCitaAsync("77");
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("77") } } };
        var impatti = new EfDocumentImpactRepository(_db);
        var uc = new SpecialAreaImportUseCase(_repo, dir, _policy,
            new DocumentImpactService(impatti, new SempreSi()));

        await uc.RunAsync();
        await uc.RunAsync();   // secondo giro, stessi dati

        Assert.Empty(await impatti.ListOpenAsync(doc));
    }

    [Fact]
    public async Task Un_Area_Che_Cambia_Nome_Segnala_I_Documenti_Che_La_Citano()
    {
        var doc = await DocumentoCheCitaAsync("77");
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("77") } } };
        var impatti = new EfDocumentImpactRepository(_db);
        var uc = new SpecialAreaImportUseCase(_repo, dir, _policy,
            new DocumentImpactService(impatti, new SempreSi()));
        await uc.RunAsync();

        dir.Areas["LIRR"] = new() { Area("77", "LI R14A (rivista)") };
        await uc.RunAsync();

        var riga = Assert.Single(await impatti.ListOpenAsync(doc));
        Assert.Equal(Vipi.Domain.ImpactKind.AreaChanged, riga.Kind);
        Assert.Equal("area:77", riga.SourceKey);
        // Le aree regolamentate non le congela nessuna release: il cambio e' gia' sotto gli occhi del pubblico.
        Assert.True(riga.IsPublicNow);
    }

    [Fact]
    public async Task Un_Area_Potata_Segnala_I_Documenti_Che_La_Citano()
    {
        var doc = await DocumentoCheCitaAsync("77");
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("77") } } };
        var impatti = new EfDocumentImpactRepository(_db);
        var uc = new SpecialAreaImportUseCase(_repo, dir, _policy,
            new DocumentImpactService(impatti, new SempreSi()));
        await uc.RunAsync();

        dir.Areas["LIRR"] = new();   // la sorgente non la elenca piu'
        await uc.RunAsync();

        var riga = Assert.Single(await impatti.ListOpenAsync(doc));
        Assert.Equal(Vipi.Domain.ImpactKind.AreaGone, riga.Kind);
    }

    /// <summary>Un documento con una sezione «regulated» che cita l'area per id: la forma con cui la
    /// selezione viene salvata davvero dall'editor.</summary>
    private async Task<int> DocumentoCheCitaAsync(string ivaoId)
    {
        var doc = new Vipi.Domain.Entities.Document
        {
            Type = Vipi.Domain.DocumentType.Vipi, Title = "vIPI Roma ACC",
            Language = Vipi.Domain.Language.It, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var ver = new Vipi.Domain.Entities.DocumentVersion
        {
            DocumentId = doc.Id, VersionNumber = 1, Status = Vipi.Domain.DocumentStatus.Draft,
            CreatedUtc = DateTime.UtcNow, AiracCycle = "2608",
        };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();

        var sec = new Vipi.Domain.Entities.DocumentSection
        {
            DocumentVersionId = ver.Id, SectionKey = "regulated", Title = "Aree regolamentate", Order = 1,
        };
        _db.DocumentSections.Add(sec);
        await _db.SaveChangesAsync();

        _db.ContentBlocks.Add(new Vipi.Domain.Entities.ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = sec.Id, Order = 0,
            BodyJson = "{\"OwnAuto\":false,\"OwnIds\":[\"" + ivaoId + "\"],\"ExtraIds\":[]}",
        });
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    private sealed class SempreSi : Vipi.Application.Auth.IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }

    private sealed class FakeAccDirectory : IAccDirectory
    {
        public Dictionary<string, List<SourceSpecialArea>> Areas { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Throw { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int Calls { get; private set; }

        /// <summary>Id per cui il chiamante ha detto di saltare il dettaglio, come li ha visti il client.</summary>
        public List<string> SkippedDetails { get; } = new();

        public Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(
            string accIcao, IReadOnlySet<string> skipDetailIds, CancellationToken ct = default)
        {
            Calls++;
            SkippedDetails.AddRange(skipDetailIds);
            if (Throw.Contains(accIcao)) throw new HttpRequestException($"specialAreas: nessuna risposta per {accIcao}.");

            // Come il client reale: per le aree in skip il dettaglio non si chiama, quindi la shape torna null.
            var all = Areas.TryGetValue(accIcao, out var a) ? a : new();
            return Task.FromResult<IReadOnlyList<SourceSpecialArea>>(
                all.Select(x => skipDetailIds.Contains(x.IvaoId) ? x with { RegionMapPolygon = null } : x).ToList());
        }

        public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
