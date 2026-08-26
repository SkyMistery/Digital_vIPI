using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Stats;
using Vipi.Ui;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La pagina della divisione: chi vede che cosa.
///
/// <para>⚠️ Il caso che conta è il socio con la <b>classifica accesa</b>. Non è un anonimo respinto sulla
/// porta: è dentro la pagina, e da lì in giù ci sono due cose che non sono sue — la ricerca per VID (porta
/// alle statistiche personali di chiunque) e il traffico coperto/scoperto di ogni scalo (strumento di
/// pianificazione dello staff). Se un giorno una delle due guardie scivola, è questo test a dirlo.</para>
/// </summary>
public class StatsDivisionPageTests : TestContext
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

    private sealed class FakeAuthz : IEditAuthorizationService
    {
        public bool IsAdmin { get; set; }
        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public Task EnsureCanEditAccAsync(string a, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string a, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanEditDocumentAsync(int d, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int u, string? n, string a, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }

    private sealed class FakeSettings : IStatsSettingsStore
    {
        public bool Pubblica { get; set; }
        public Task<StatsSettings> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(StatsSettings.Default with { PublicLeaderboard = Pubblica });
        public Task SaveAsync(bool p, int u, CancellationToken ct = default) { Pubblica = p; return Task.CompletedTask; }
    }

    private sealed class ArchivioVuoto : IAtcStatsQueries
    {
        /// <summary>Quante volte sono stati chiesti i totali: dice se la pagina è tornata a leggere.</summary>
        public int Letture { get; private set; }

        public Task<StatsTotals> TotalsAsync(int? u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default)
        {
            Letture++;
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
        public Task<StatsRank> RankAsync(int u, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Task.FromResult(new StatsRank(0, 3));
        public Task<DateTimeOffset?> ArchiveStartAsync(int? u, CancellationToken ct = default) => Task.FromResult<DateTimeOffset?>(null);

        private static Task<IReadOnlyList<T>> Vuoto<T>() => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
    }

    private sealed class RosterFinto : IStaffRosterRepository
    {
        public Task<StaffRosterEntry?> FindAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<StaffRosterEntry?>(null);

        public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
        public Task UpsertLoginAsync(int u, string? n, IReadOnlyList<string> p, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StaffRosterEntry>>(Array.Empty<StaffRosterEntry>());
        public Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task UpdateVerifiedAsync(int u, string? n, string? r, IReadOnlyList<string> p, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateAsync(int u, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>
    /// ⚠️ Esplode a ogni metodo, di proposito: la guardia del traffico d'aeroporto deve stare <b>prima</b>
    /// della query. Nasconderlo nel markup lo avrebbe comunque già tirato fuori dal database.
    /// </summary>
    private sealed class NessunaLetturaAeroporti : IAirportCoverageQueries
    {
        public Task<AirportCoverageSummary> ByAirportAsync(DateTimeOffset f, DateTimeOffset t, string? acc = null, CancellationToken ct = default) =>
            throw new InvalidOperationException("query eseguita: la guardia è arrivata tardi");
        public Task<IReadOnlyList<(string Code, string Name, int Airports)>> GroupsAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("query eseguita: la guardia è arrivata tardi");
    }

    private sealed class AeroportiFinti : IAirportCoverageQueries
    {
        public List<string?> GruppiChiesti { get; } = new();

        public Task<AirportCoverageSummary> ByAirportAsync(DateTimeOffset f, DateTimeOffset t, string? acc = null, CancellationToken ct = default)
        {
            GruppiChiesti.Add(acc);
            return Task.FromResult(new AirportCoverageSummary(
                new[]
                {
                    new AirportCoverageRow("LIRF", "Fiumicino", "LIRR", 400, 100, 600, 1440),
                    new AirportCoverageRow("LIRA", "Ciampino", "LIRR", 50, 0, 0, 1440),
                },
                450, 100, 600, f, 1, 1));
        }

        public Task<IReadOnlyList<(string Code, string Name, int Airports)>> GroupsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<(string, string, int)>>(new[] { ("LIRR", "Roma", 42), ("LIMM", "Milano", 25) });
    }

    private readonly ArchivioVuoto _archivio = new();

    private IRenderedComponent<StatsDivisionPage> Render(
        bool staff, bool classificaPubblica, IAirportCoverageQueries aeroporti, string? gruppo = null)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IAtcStatsQueries>(_archivio);
        Services.AddSingleton<IStatsSettingsStore>(new FakeSettings { Pubblica = classificaPubblica });
        Services.AddSingleton<ICurrentUserProvider>(new FakeUser
        {
            User = new CurrentUser(704798, "Chi Guarda", "LIRR", staff ? new[] { "IT-AOC" } : Array.Empty<string>()),
        });
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz { IsAdmin = staff });
        Services.AddSingleton<IStaffRosterRepository>(new RosterFinto());
        Services.AddSingleton(aeroporti);

        // ⚠️ Un parametro `[SupplyParameterFromQuery]` non si passa a mano: bUnit lo rifiuta e chiede di
        // navigare, che è anche il modo in cui la pagina lo riceve davvero.
        if (gruppo is not null)
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
                .NavigateTo($"http://localhost/services/stats/division?g={gruppo}");

        return RenderComponent<StatsDivisionPage>();
    }

    [Fact]
    public void Lo_staff_vede_il_traffico_degli_aeroporti_e_il_totale()
    {
        var cut = Render(staff: true, classificaPubblica: false, new AeroportiFinti());

        Assert.Contains("Stats_Airports", cut.Markup);
        Assert.Contains("LIRF", cut.Markup);
        Assert.Contains("Fiumicino", cut.Markup);
        // 100 su 400 = 25%, e il totale 100 su 450 = 22%.
        Assert.Contains("100 · 25%", cut.Markup);
        Assert.Contains("100 · 22%", cut.Markup);
    }

    /// <summary>
    /// ⚠️ Un socio con la classifica accesa è DENTRO la pagina: la sezione aeroporti non deve solo essere
    /// nascosta, la sua query non deve nemmeno partire.
    /// </summary>
    [Fact]
    public void Un_socio_non_vede_il_traffico_degli_aeroporti_e_la_query_non_parte()
    {
        var cut = Render(staff: false, classificaPubblica: true, new NessunaLetturaAeroporti());

        Assert.Contains("Stats_TopControllers", cut.Markup);      // la classifica sì, è pubblica
        Assert.DoesNotContain("Stats_Airports", cut.Markup);      // gli aeroporti no
    }

    // ⚠️ Due test e non uno con due `Render`: bUnit congela il contenitore dei servizi al primo uso, e un
    // secondo `Render` nello stesso caso esplode con «services cannot be registered after…».
    [Fact]
    public void Un_socio_non_puo_cercare_le_statistiche_di_un_altro()
    {
        var cut = Render(staff: false, classificaPubblica: true, new NessunaLetturaAeroporti());
        Assert.Empty(cut.FindAll(".vid-find"));
    }

    [Fact]
    public void Lo_staff_puo_cercare_per_vid()
    {
        var cut = Render(staff: true, classificaPubblica: false, new AeroportiFinti());
        Assert.Single(cut.FindAll(".vid-find"));
    }

    /// <summary>
    /// ⚠️ Le due file di chip non si cancellano a vicenda: cambiare il periodo deve conservare il gruppo,
    /// o chi guarda Milano torna su tutta l'Italia senza capire perché.
    /// </summary>
    [Fact]
    public void Le_chip_del_periodo_conservano_il_gruppo_scelto()
    {
        var cut = Render(staff: true, classificaPubblica: false, new AeroportiFinti(), gruppo: "LIMM");

        var indirizzi = cut.FindAll("nav.chipbar a.chip-link").Select(a => a.GetAttribute("href")!).ToList();
        var periodi = indirizzi.Where(h => h.Contains("p=")).ToList();

        Assert.NotEmpty(periodi);
        Assert.All(periodi.Take(StatsView.Periods.Count), h => Assert.Contains("g=LIMM", h));
    }

    /// <summary>
    /// ⚠️ Il difetto segnalato dal committente il 25 agosto 2026, e la ragione per cui questa pagina carica
    /// in <c>OnParametersSetAsync</c>: è l'unica delle tre <b>interattiva</b>, e su un componente
    /// interattivo l'inizializzazione gira <b>una volta sola</b>. Le chip sono link che cambiano la sola
    /// stringa di query — l'indirizzo cambiava, la chip si accendeva, e i numeri restavano quelli di prima.
    /// </summary>
    [Fact]
    public void Premere_una_chip_fa_rileggere_i_numeri()
    {
        var aeroporti = new AeroportiFinti();
        Render(staff: true, classificaPubblica: false, aeroporti);

        var primeLetture = _archivio.Letture;
        Assert.True(primeLetture > 0);

        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo("http://localhost/services/stats/division?p=30&g=LIMM");

        Assert.True(_archivio.Letture > primeLetture, "cambiando la stringa di query i numeri non sono stati riletti");
        Assert.Contains("LIMM", aeroporti.GruppiChiesti);
    }

    /// <summary>
    /// ...ma non a ogni render: la chiave dell'ultimo caricamento evita di rifare tutte le query quando i
    /// parametri non sono cambiati (l'interruttore della classifica ne provoca parecchi).
    /// </summary>
    [Fact]
    public void Un_render_senza_parametri_nuovi_non_rifa_le_query()
    {
        var cut = Render(staff: true, classificaPubblica: false, new AeroportiFinti());

        var primeLetture = _archivio.Letture;
        cut.Render();

        Assert.Equal(primeLetture, _archivio.Letture);
    }

    [Fact]
    public void Il_gruppo_dell_indirizzo_arriva_alla_query()
    {
        var aeroporti = new AeroportiFinti();
        Render(staff: true, classificaPubblica: false, aeroporti, gruppo: "lirr");

        Assert.Equal(new string?[] { "LIRR" }, aeroporti.GruppiChiesti.ToArray());
    }
}
