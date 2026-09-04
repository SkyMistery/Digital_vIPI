using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Policy di import globale (opt-out): le categorie importate sono in sola lettura (guard nei service),
/// l'import salta le categorie escluse, e lo store ha default "tutto importato".
/// </summary>
public class ImportPolicyTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfImportPolicyStore _store = default!;

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? User { get; set; }
        public CurrentUser? Get() => User;
    }

    private sealed class FakeDirectory : IAirportDirectory
    {
        public List<SourceAirport> Airports { get; } = new();
        public Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<SourceAirport>)Airports);
        public Task<SourceAirport?> GetByIcaoAsync(string icao, CancellationToken ct = default)
            => Task.FromResult(Airports.FirstOrDefault(a => string.Equals(a.Icao, icao, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class FakeDetails : IAirportDetailProvider
    {
        public List<SourceAtcPosition> Positions { get; } = new();
        public List<SourceRunway> Runways { get; } = new();
        public Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<SourceAtcPosition>)Positions);
        public Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default)
            => Task.FromResult(Positions.FirstOrDefault(p => p.Callsign == composePosition));
        public Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<SourceRunway>)Runways);
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfImportPolicyStore(_db);

        var structRepo = new EfStructureEditingRepository(_db);
        await structRepo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await structRepo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private AirportEditingService BuildService(FakeDirectory? dir = null, FakeDetails? det = null)
    {
        var provider = new FakeUser { User = new CurrentUser(1, "Admin", "LIRR", new[] { "IT-AOC" }) };
        var authz = new EditAuthorizationService(provider,
            new Vipi.Application.Auth.RoleResolver(new Vipi.Application.Auth.AuthOptions(), new Vipi.Application.DivisionOptions()), SenzaPromozioni.Instance);
        return new AirportEditingService(new EfAirportRepository(_db, new EfMediaMaintenance(_db)), authz,
            dir ?? new FakeDirectory(), det ?? new FakeDetails(), _store);
    }

    [Fact]
    public async Task Store_Defaults_To_All_Imported_And_RoundTrips()
    {
        var def = await _store.GetAsync();
        Assert.True(def is { TransitionAltitude: true, Runways: true, Sectors: true });

        await _store.SaveAsync(new ImportPolicySnapshot(false, false, true), updatedByUserId: 42);
        var saved = await _store.GetAsync();
        Assert.False(saved.TransitionAltitude);
        Assert.False(saved.Runways);
        Assert.True(saved.Sectors);
    }

    [Fact]
    public async Task TA_Locked_By_Default_Rejects_Edit_But_Allows_When_Excluded()
    {
        var svc = BuildService();

        // Default: TA importata e bloccata → scrittura rifiutata.
        await Assert.ThrowsAsync<ValidationException>(() => svc.SetTransitionAltitudeAsync("LIRF", 6000));

        // Escludendo TA, la scrittura passa e persiste.
        await _store.SaveAsync(new ImportPolicySnapshot(false, true, true), 1);
        await svc.SetTransitionAltitudeAsync("LIRF", 6000);
        var ta = (await _db.Airports.AsNoTracking().FirstAsync(a => a.Icao == "LIRF")).TransitionAltitudeFt;
        Assert.Equal(6000, ta);
    }

    [Fact]
    public async Task Runways_Locked_Rejects_Geometry_Change_But_Allows_Editorial()
    {
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        await profile.MergeFromSourceAsync("LIRF", null, new[] { new SourceRunway("16L", 3902, 160) });
        var svc = BuildService();
        var stored = (await profile.LoadAsync("LIRF"))!.Runways.Single();

        // Cambiare ident con piste bloccate → rifiutato.
        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveRunwaysAsync("LIRF", new[]
        {
            stored with { Ident = "16R" },
        }));

        // Solo colonne editoriali (ident/lunghezza/bearing invariati) → consentito.
        await svc.SaveRunwaysAsync("LIRF", new[] { stored with { ToraM = "3000" } });
        Assert.Equal("3000", (await profile.LoadAsync("LIRF"))!.Runways.Single().ToraM);
    }

    /// <summary>
    /// ⚠️ A piste bloccate una pista NUOVA non si inventa, ma una si può TOGLIERE. Sembra un'incoerenza e non
    /// lo è: quando IVAO ri-denomina uno scalo (Rimini 13/31 → 12/30) le piste morte che portano TORA/LDA
    /// restano in archivio apposta — il merge non distrugge lavoro editoriale — e qualcuno deve poterle
    /// togliere. Vietarlo qui chiudeva l'amministratore fuori dal suo archivio: la ✕ compariva solo a policy
    /// spenta, e la policy è GLOBALE, quindi per ripulire un aeroporto si sbloccavano tutti gli altri.
    /// <para>L'asimmetria sta nel rimedio: togliere per sbaglio una pista viva dura fino al re-import
    /// successivo, che la rimette. Un'aggiunta a mano invece non si ripara da sé.</para>
    /// </summary>
    [Fact]
    public async Task Runways_Locked_Allows_Removal_But_Not_Addition()
    {
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        await profile.MergeFromSourceAsync("LIRF", null,
            new[] { new SourceRunway("16L", 3902, 160), new SourceRunway("34R", 3902, 340) });
        var svc = BuildService();
        var stored = (await profile.LoadAsync("LIRF"))!.Runways;

        // Aggiungere una pista → rifiutato.
        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveRunwaysAsync("LIRF",
            stored.Append(new RunwayRow(0, "07", 1500, 70, null, null, null, null, null)).ToList()));

        // Toglierne una → consentito.
        await svc.SaveRunwaysAsync("LIRF", new[] { stored.Single(r => r.Ident == "16L") });
        Assert.Equal(new[] { "16L" }, (await profile.LoadAsync("LIRF"))!.Runways.Select(r => r.Ident).ToArray());
    }

    [Fact]
    public async Task Reimport_Skips_Excluded_Categories()
    {
        var profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        // Stato iniziale editoriale dell'utente: TA 4000 + pista 16L lunga 3902.
        await profile.MergeFromSourceAsync("LIRF", 4000, new[] { new SourceRunway("16L", 3902, 160) });

        // Escludo TA e Piste → reimport non deve toccarle, anche se la sorgente fornisce valori diversi.
        await _store.SaveAsync(new ImportPolicySnapshot(false, false, true), 1);
        var dir = new FakeDirectory();
        dir.Airports.Add(new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, 9000));
        var det = new FakeDetails();
        det.Runways.Add(new SourceRunway("16L", 9999, 160));

        await BuildService(dir, det).ReimportFromSourceAsync("LIRF");

        var data = await profile.LoadAsync("LIRF");
        Assert.Equal(4000, data!.TransitionAltitudeFt);            // TA esclusa: invariata
        Assert.Equal(3902, data.Runways.Single().LengthM);         // Piste escluse: invariate
    }

    /// <summary>
    /// Il cambio di policy va nel registro: decide quali dati la sorgente può sovrascrivere, ed è l'atto che
    /// qualcuno andrà a cercare il giorno in cui un dato smette di aggiornarsi. Nella riga stanno le sole
    /// categorie <b>cambiate</b>, divise per verso.
    /// </summary>
    [Fact]
    public async Task Changing_the_policy_is_written_to_the_audit_log()
    {
        await _store.SaveAsync(new ImportPolicySnapshot(true, Runways: false, true, Sids: false, true), 704798);

        var riga = await _db.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(704798, riga.UserId);
        Assert.Equal("ImportPolicy", riga.EntityType);
        Assert.Contains("Runways", riga.DetailsJson);
        Assert.Contains("Sids", riga.DetailsJson);
        Assert.DoesNotContain("Sectors", riga.DetailsJson);        // non è cambiata: non se ne parla
    }

    /// <summary>
    /// ⚠️ Il non-evento non si scrive: un salvataggio che non cambia niente non è un atto, e riscriverebbe
    /// «deciso da X» su una decisione che aveva preso qualcun altro.
    /// </summary>
    [Fact]
    public async Task Saving_the_same_policy_writes_nothing()
    {
        await _store.SaveAsync(new ImportPolicySnapshot(true, false, true, true, true), 1);
        var quando = (await _db.ImportPolicies.AsNoTracking().SingleAsync()).UpdatedUtc;

        await _store.SaveAsync(new ImportPolicySnapshot(true, false, true, true, true), 999);

        Assert.Equal(1, await _db.AuditLogs.CountAsync());
        var riga = await _db.ImportPolicies.AsNoTracking().SingleAsync();
        Assert.Equal(1, riga.UpdatedByUserId);                     // l'autore resta chi ha deciso davvero
        Assert.Equal(quando, riga.UpdatedUtc);
    }

    /// <summary>
    /// La prima scrittura registra CHI, anche quando i valori coincidono col default: è l'unica cosa che
    /// distingue una policy decisa da una nata dai default delle colonne (il caso <c>ImportSids</c>).
    /// </summary>
    [Fact]
    public async Task The_first_save_records_the_author_even_without_changes()
    {
        await _store.SaveAsync(ImportPolicySnapshot.AllImported, 704798);

        Assert.Equal(704798, (await _db.ImportPolicies.AsNoTracking().SingleAsync()).UpdatedByUserId);
        Assert.Equal(0, await _db.AuditLogs.CountAsync());         // nessuna categoria è cambiata
    }

    /// <summary>
    /// Le tre letture della riga singola sono ORDINATE.
    ///
    /// <para>⚠️ Questo test esiste per un avviso vero, letto nel log del 25 agosto 2026:
    /// <c>FirstWithoutOrderByAndFilterWarning</c> (EF Query 10103). Innocuo com'era — la tabella ha una riga sola per
    /// convenzione — ma «la prima riga senza ordine» e' quella che sceglie il motore, e quella riga decide
    /// il regime di scrittura di tutta l'applicazione. Qui l'avviso e' alzato a ECCEZIONE: chi rimettesse un
    /// <c>FirstOrDefault</c> nudo trova il rosso qui, non un avviso in mezzo a mille righe di log.</para>
    /// </summary>
    [Fact]
    public async Task Le_letture_della_riga_singola_non_alzano_l_avviso_di_EF()
    {
        var opzioni = new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite(_conn)
            .ConfigureWarnings(w => w.Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning))
            .Options;

        await using var severo = new VipiDbContext(opzioni);
        var store = new EfImportPolicyStore(severo);

        // A tabella vuota e con la riga scritta: sono due query compilate diverse.
        await store.GetAsync();
        await store.GetInfoAsync();
        await store.SaveAsync(ImportPolicySnapshot.AllImported, 704798);
        await store.GetAsync();
    }
}
