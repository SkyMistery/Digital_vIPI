using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I pattern degli staff code admin: dal 22 agosto 2026 il lato divisione è un jolly (<c>^IT-[A-Z0-9]+$</c>,
/// formato osservato davvero contro l'API), mentre il lato chief ACC resta un'<b>ipotesi</b> mai vista in
/// un login vero. Se sbagliano, i due esiti non si somigliano — «nessuno è admin» blocca
/// tutti fuori senza rumore e non si rimedia da dentro (assegnare permessi richiede di essere admin);
/// «troppi admin» regala il controllo editoriale. Questi test coprono la diagnosi che rende il primo caso
/// visibile, e il caso in cui non deve suonare.
/// </summary>
public class AdminCoverageTests
{
    private sealed class RosterFinto : IStaffRosterRepository
    {
        public Task<StaffRosterEntry?> FindAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<StaffRosterEntry?>(null);

        private readonly List<StaffRosterEntry> _righe;
        public RosterFinto(params (int Vid, string[] Codes)[] righe) =>
            _righe = righe.Select(r => new StaffRosterEntry(r.Vid, $"Tizio {r.Vid}", "ACC", r.Codes, DateTime.UtcNow)).ToList();

        public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StaffRosterEntry>>(_righe);

        public Task UpsertLoginAsync(int userId, string? displayName, IReadOnlyList<string> positions, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task UpdateVerifiedAsync(int userId, string? displayName, string? atcRating, IReadOnlyList<string> positions, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
    }

    private static AdminCoverageService Servizio(IStaffRosterRepository roster) =>
        new(roster, new RoleResolver(new AuthOptions(), new DivisionOptions()));

    [Fact]
    public async Task Un_codice_di_divisione_vero_risulta_admin()
    {
        // ⚠️ Dal 28 agosto 2026 «IT-AOA1» e «IT-T03» NON sono più admin: sono staff di divisione. Chi lo è
        // qui è «IT-AOC», uno degli otto codici di direzione — ed è il senso del cambio di regola.
        var c = await Servizio(new RosterFinto((704798, new[] { "IT-AOA1", "IT-AOC" }))).DescribeAsync();

        Assert.True(c.AnyAdmin);
        Assert.Equal(new[] { "IT-AOC" }, c.Rows.Single().Matched);
        Assert.Equal(new[] { "IT-AOA1" }, c.UnmatchedCodes);   // resta visibile: e' staff, ma non e' admin
    }

    /// <summary>
    /// I codici che non danno l'admin restano fuori <b>e visibili</b>: è l'elenco da cui si capisce se un
    /// pattern è sbagliato, e serve che ci sia dentro tanto un <c>DE-DIR</c> (staff di un'altra divisione)
    /// quanto un <c>IT-AOA1</c> (staff nostro, ma non di direzione).
    /// </summary>
    [Fact]
    public async Task I_codici_che_non_danno_ladmin_restano_fuori_e_si_vedono()
    {
        var c = await Servizio(new RosterFinto((704798, new[] { "IT-AOC", "IT-AOA1", "DE-DIR" }))).DescribeAsync();

        Assert.True(c.AnyAdmin);
        Assert.Equal(new[] { "IT-AOC" }, c.Rows.Single().Matched);
        Assert.Equal(new[] { "DE-DIR", "IT-AOA1" }, c.UnmatchedCodes);   // restano visibili, non spariscono
    }

    /// <summary>
    /// ⚠️ Dal 28 agosto 2026 il chief d'ACC <b>non è admin</b>: è <c>Editor</c>, cioè cura i documenti e non
    /// distribuisce permessi. Questa diagnosi guarda il solo livello admin — quello che, mancando, non si
    /// ripara da dentro — quindi un roster fatto di soli chief deve risultare <b>senza admin</b>.
    /// </summary>
    [Fact]
    public async Task Il_chief_di_un_acc_non_e_admin()
    {
        var c = await Servizio(new RosterFinto((123, new[] { "LIRR-CH" }))).DescribeAsync();

        Assert.False(c.AnyAdmin);
        Assert.Equal(new[] { "LIRR-CH" }, c.UnmatchedCodes);
    }

    [Fact]
    public async Task Se_nessun_codice_combacia_la_diagnosi_lo_dice_e_mostra_i_codici_visti()
    {
        // Forme plausibili ma diverse da quelle configurate: è esattamente il modo in cui il guasto si presenta.
        var svc = Servizio(new RosterFinto((1, new[] { "IT-AOA-1" }), (2, new[] { "ITWM" })));

        var c = await svc.DescribeAsync();
        Assert.False(c.AnyAdmin);

        var f = Assert.Single(await svc.RunAsync());
        Assert.Equal(ConsistencySeverity.Error, f.Severity);
        Assert.Contains("IT-AOA-1", f.Detail);
        Assert.Contains("ITWM", f.Detail);      // i codici veri finiscono nel messaggio: è da lì che si corregge
    }

    /// <summary>
    /// Il caso che <b>non</b> deve suonare: su un'installazione appena nata nessuno ha ancora fatto login, e
    /// il roster si popola proprio dai login. Segnalarlo lì riempirebbe di rumore il momento in cui il rumore
    /// serve meno — e renderebbe Degraded ogni deploy nuovo.
    /// </summary>
    [Fact]
    public async Task Con_il_roster_vuoto_non_si_segnala_nulla()
    {
        var svc = Servizio(new RosterFinto());

        Assert.True((await svc.DescribeAsync()).RosterEmpty);
        Assert.Empty(await svc.RunAsync());
    }

    [Fact]
    public async Task Con_almeno_un_admin_non_si_segnala_nulla()
    {
        var svc = Servizio(new RosterFinto((1, new[] { "IT-DIR" }), (2, new[] { "IT-T03" })));
        Assert.Empty(await svc.RunAsync());
    }

    /// <summary>
    /// La diagnosi deve usare gli STESSI pattern dell'autorizzazione, non una copia: se l'override esplicito
    /// è configurato, vince quello anche qui. Altrimenti direbbe «tutto a posto» mentre chi decide usa altro.
    /// </summary>
    [Fact]
    public async Task L_override_esplicito_vale_anche_per_la_diagnosi()
    {
        var auth = new AuthOptions { AdminStaffCodes = new List<string> { "^ZZ-BOSS$" } };
        var svc = new AdminCoverageService(new RosterFinto((9, new[] { "IT-DIR" }), (10, new[] { "ZZ-BOSS" })),
            new RoleResolver(auth, new DivisionOptions()));

        var c = await svc.DescribeAsync();
        Assert.Equal(new[] { "^ZZ-BOSS$" }, c.Patterns);
        Assert.Equal(new[] { "ZZ-BOSS" }, c.Rows.Single(r => r.UserId == 10).Matched);
        Assert.Empty(c.Rows.Single(r => r.UserId == 9).Matched);   // IT-DIR non vale più: l'override sostituisce
    }
}
