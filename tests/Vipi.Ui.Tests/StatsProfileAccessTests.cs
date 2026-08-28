using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Stats;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;
using Vipi.Domain;

namespace Vipi.Ui.Tests;

/// <summary>
/// Chi può aprire le statistiche personali di un <b>altro</b> controllore, e cosa resta scritto quando lo fa.
///
/// <para>La pagina è una sola per due indirizzi: <c>/services/stats</c> (le proprie) e
/// <c>/services/stats/user/{vid}</c> (quelle di un altro, solo staff). È la scelta che tiene i due casi
/// allineati, ed è anche quella che rende necessaria questa rete: la guardia non è più «una pagina in una
/// cartella admin», è un <c>if</c> dentro un metodo, e un <c>if</c> si sposta per sbaglio.</para>
/// </summary>
public class StatsProfileAccessTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join(",", arguments), false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? User { get; set; }
        public CurrentUser? Get() => User;
    }

    /// <summary>Autorizzazione ridotta all'osso: alla pagina interessa <c>IsAdmin</c> e nient'altro.</summary>
    private sealed class FakeAuthz : IEditAuthorizationService
    {
        public bool IsAdmin { get; set; }
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public void EnsureAdmin() { }
    }

    private sealed class FakeSettings : IStatsSettingsStore
    {
        public Task<StatsSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(StatsSettings.Default);
        public Task SaveAsync(bool p, int u, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>
    /// ⚠️ Ogni lettura è un'eccezione, di proposito. Il test sul divieto non deve solo verificare che la
    /// pagina scriva «non ti è permesso»: deve verificare che <b>nessuna query sia partita</b>. Una guardia
    /// messa dopo le letture nasconderebbe i numeri a schermo e li avrebbe comunque tirati fuori dal DB.
    /// </summary>
    private sealed class NessunaLettura : IAtcStatsQueries
    {
        private static T Boom<T>() => throw new InvalidOperationException("query eseguita: la guardia è arrivata tardi");

        public Task<StatsTotals> TotalsAsync(int? u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Boom<Task<StatsTotals>>();
        public Task<IReadOnlyList<StatsByKey>> ByPositionAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 20, CancellationToken ct = default) => Boom<Task<IReadOnlyList<StatsByKey>>>();
        public Task<IReadOnlyList<StatsByKey>> ByMonthAsync(int? u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Boom<Task<IReadOnlyList<StatsByKey>>>();
        public Task<IReadOnlyList<StatsSessionRow>> SessionsAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 50, CancellationToken ct = default) => Boom<Task<IReadOnlyList<StatsSessionRow>>>();
        public Task<StatsSessionDetail?> SessionAsync(long id, CancellationToken ct = default) => Boom<Task<StatsSessionDetail?>>();
        public Task<IReadOnlyList<ControllerRanking>> TopControllersAsync(DateTimeOffset f, DateTimeOffset t, int l = 20, CancellationToken ct = default) => Boom<Task<IReadOnlyList<ControllerRanking>>>();
        public Task<IReadOnlyList<CoverageCell>> CoverageAsync(int? u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Boom<Task<IReadOnlyList<CoverageCell>>>();
        public Task<IReadOnlyList<StatsByKey>> TopAirportsAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 15, CancellationToken ct = default) => Boom<Task<IReadOnlyList<StatsByKey>>>();
        public Task<IReadOnlyList<StatsByKey>> ManagedAirportsAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 15, CancellationToken ct = default) => Boom<Task<IReadOnlyList<StatsByKey>>>();
        public Task<IReadOnlyList<StatsByKey>> TopAircraftAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 15, CancellationToken ct = default) => Boom<Task<IReadOnlyList<StatsByKey>>>();
        public Task<StatsStreak> StreakAsync(int u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Boom<Task<StatsStreak>>();
        public Task<StatsRank> RankAsync(int u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Boom<Task<StatsRank>>();
        public Task<DateTimeOffset?> ArchiveStartAsync(int? u, CancellationToken ct = default) => Boom<Task<DateTimeOffset?>>();
    }

    /// <summary>Archivio vuoto, ma che ANNOTA di chi gli sono stati chiesti i numeri.</summary>
    private sealed class ArchivioVuoto : IAtcStatsQueries
    {
        public List<int?> VidChiesti { get; } = new();

        public Task<StatsTotals> TotalsAsync(int? u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default)
        {
            VidChiesti.Add(u);
            return Task.FromResult(new StatsTotals(0, 0, 0, 0, 0));
        }
        public Task<IReadOnlyList<StatsByKey>> ByPositionAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 20, CancellationToken ct = default) => Vuoto<StatsByKey>();
        public Task<IReadOnlyList<StatsByKey>> ByMonthAsync(int? u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Vuoto<StatsByKey>();
        public Task<IReadOnlyList<StatsSessionRow>> SessionsAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 50, CancellationToken ct = default) => Vuoto<StatsSessionRow>();
        public Task<StatsSessionDetail?> SessionAsync(long id, CancellationToken ct = default) => Task.FromResult<StatsSessionDetail?>(null);
        public Task<IReadOnlyList<ControllerRanking>> TopControllersAsync(DateTimeOffset f, DateTimeOffset t, int l = 20, CancellationToken ct = default) => Vuoto<ControllerRanking>();
        public Task<IReadOnlyList<CoverageCell>> CoverageAsync(int? u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Vuoto<CoverageCell>();
        public Task<IReadOnlyList<StatsByKey>> TopAirportsAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 15, CancellationToken ct = default) => Vuoto<StatsByKey>();
        public Task<IReadOnlyList<StatsByKey>> ManagedAirportsAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 15, CancellationToken ct = default) => Vuoto<StatsByKey>();
        public Task<IReadOnlyList<StatsByKey>> TopAircraftAsync(int? u, DateTimeOffset f, DateTimeOffset t, int l = 15, CancellationToken ct = default) => Vuoto<StatsByKey>();
        public Task<StatsStreak> StreakAsync(int u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Task.FromResult(new StatsStreak(0, 0, null));
        public Task<StatsRank> RankAsync(int u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Task.FromResult(new StatsRank(0, 0));
        public Task<DateTimeOffset?> ArchiveStartAsync(int? u, CancellationToken ct = default) => Task.FromResult<DateTimeOffset?>(null);

        private static Task<IReadOnlyList<T>> Vuoto<T>() => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
    }

    private sealed class RegistroFinto : IStatsAccessLog
    {
        public List<(int Attore, int Soggetto)> Accessi { get; } = new();
        public Task RecordProfileViewAsync(int attore, int soggetto, CancellationToken ct = default)
        {
            if (attore != soggetto) Accessi.Add((attore, soggetto));
            return Task.CompletedTask;
        }
    }

    private sealed class RosterFinto : IStaffRosterRepository
    {
        public Task<StaffRosterEntry?> FindAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<StaffRosterEntry?>(null);

        public Dictionary<int, string> Nomi { get; } = new();
        public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(
                Nomi.Where(k => ids.Contains(k.Key)).ToDictionary(k => k.Key, k => k.Value));

        public Task UpsertLoginAsync(int u, string? n, IReadOnlyList<string> p, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StaffRosterEntry>>(Array.Empty<StaffRosterEntry>());
        public Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task UpdateVerifiedAsync(int u, string? n, string? r, IReadOnlyList<string> p, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateAsync(int u, CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly RegistroFinto _registro = new();
    private readonly RosterFinto _roster = new();

    private IRenderedComponent<StatsHome> Render(bool staff, int? vidGuardato, IAtcStatsQueries archivio,
                                                 int io = 704798)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IAtcStatsQueries>(archivio);
        Services.AddSingleton<IStatsSettingsStore>(new FakeSettings());
        Services.AddSingleton<ICurrentUserProvider>(new FakeUser
        {
            User = new CurrentUser(io, "Chi Guarda", "LIRR", staff ? new[] { "IT-AOC" } : Array.Empty<string>()),
        });
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz { IsAdmin = staff });
        Services.AddSingleton<IStatsAccessLog>(_registro);
        Services.AddSingleton<IStaffRosterRepository>(_roster);

        return RenderComponent<StatsHome>(p =>
        {
            if (vidGuardato is { } v) p.Add(c => c.Vid, v);
        });
    }

    /// <summary>
    /// ⚠️ Il caso che conta: un socio qualunque scrive l'indirizzo a mano. Non deve vedere niente — e
    /// nemmeno il database deve essere interrogato.
    /// </summary>
    [Fact]
    public void Un_non_staff_non_apre_le_statistiche_di_un_altro()
    {
        var cut = Render(staff: false, vidGuardato: 555003, new NessunaLettura());

        Assert.Contains("Stats_ProfileForbidden", cut.Markup);
        Assert.DoesNotContain("Stats_StaffViewingTitle", cut.Markup);   // niente fascia: non sta guardando niente
        Assert.Empty(_registro.Accessi);                                // e non si registra un accesso mai avvenuto
    }

    /// <summary>Lo staff entra, e la pagina dice a chiare lettere di chi sono i numeri.</summary>
    [Fact]
    public void Lo_staff_apre_le_statistiche_di_un_altro_e_la_pagina_lo_dichiara()
    {
        _roster.Nomi[555003] = "Mario Rossi";
        var archivio = new ArchivioVuoto();

        var cut = Render(staff: true, vidGuardato: 555003, archivio);

        Assert.Contains("Stats_StaffViewingTitle", cut.Markup);
        Assert.Contains("Mario Rossi", cut.Markup);
        Assert.Contains("555003", cut.Markup);
        Assert.DoesNotContain("Stats_ProfileForbidden", cut.Markup);
        Assert.Contains(555003, archivio.VidChiesti);       // i numeri sono i SUOI, non i miei
    }

    /// <summary>La riga di audit si scrive: è ciò che la fascia promette a chi viene guardato.</summary>
    [Fact]
    public void Aprire_le_statistiche_di_un_altro_lascia_traccia()
    {
        Render(staff: true, vidGuardato: 555003, new ArchivioVuoto(), io: 704798);

        Assert.Equal((704798, 555003), Assert.Single(_registro.Accessi));
    }

    /// <summary>
    /// Le proprie restano le proprie: nessuna fascia, nessuna riga di audit — nemmeno per uno staffista, e
    /// nemmeno arrivandoci per l'indirizzo lungo col proprio VID.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(704798)]
    public void Le_proprie_statistiche_non_sono_un_accesso_ai_dati_di_un_altro(int? vid)
    {
        var cut = Render(staff: true, vidGuardato: vid, new ArchivioVuoto(), io: 704798);

        Assert.DoesNotContain("Stats_StaffViewingTitle", cut.Markup);
        Assert.Contains("Stats_Title", cut.Markup);
        Assert.Empty(_registro.Accessi);
    }

    /// <summary>
    /// ⚠️ I chip del periodo devono restare sulla persona guardata: con l'indirizzo fisso «/services/stats»
    /// il primo chip premuto riportava lo staffista sulle PROPRIE statistiche, in silenzio.
    /// </summary>
    [Fact]
    public void I_chip_del_periodo_restano_sulla_persona_guardata()
    {
        var cut = Render(staff: true, vidGuardato: 555003, new ArchivioVuoto());

        var indirizzi = cut.FindAll("nav.chipbar a.chip-link").Select(a => a.GetAttribute("href")!).ToList();
        Assert.NotEmpty(indirizzi);
        Assert.All(indirizzi, h => Assert.StartsWith("/services/stats/user/555003?p=", h));
    }

    /// <summary>
    /// ⚠️ Il tasto sta sulla riga del titolo e non in fondo alla pagina, ed è il difetto che il committente
    /// ha segnalato il 25 agosto 2026: chi poteva entrare non sapeva di poterlo. Se torna in coda al
    /// disclaimer, questo test non se ne accorge — ma se sparisce del tutto, sì.
    /// </summary>
    [Fact]
    public void Lo_staff_trova_il_tasto_della_divisione_sulla_riga_del_titolo()
    {
        var cut = Render(staff: true, vidGuardato: null, new ArchivioVuoto());

        var tasto = cut.Find(".sh-title a.sh-go");
        Assert.Equal("/services/stats/division", tasto.GetAttribute("href"));
    }

    /// <summary>
    /// A classifica spenta un socio qualunque non ha niente da aprire: il tasto non c'è. Offrirlo e poi
    /// negare la pagina è la promessa che il §6 vieta.
    /// </summary>
    [Fact]
    public void Senza_permesso_e_a_classifica_spenta_il_tasto_non_c_e()
    {
        var cut = Render(staff: false, vidGuardato: null, new ArchivioVuoto());
        Assert.Empty(cut.FindAll(".sh-title a.sh-go"));
    }

    /// <summary>
    /// L'esportazione CSV è stata tolta il 25 agosto 2026 su richiesta del committente: tasto <b>e</b>
    /// meccanismo. Il test resta e cambia mestiere — prima sorvegliava che non si offrisse l'esportazione
    /// <i>altrui</i>, ora che non se ne offra <b>nessuna</b>, da nessuna delle due pagine.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(555003)]
    public void L_esportazione_csv_non_si_offre_piu(int? vidGuardato)
    {
        var cut = Render(staff: true, vidGuardato: vidGuardato, new ArchivioVuoto());
        Assert.DoesNotContain("export.csv", cut.Markup);
    }
}
