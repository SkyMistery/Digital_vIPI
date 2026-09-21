using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// 🔴 Il roster degli staffisti accoglie anche i codici d'ACC, non solo quelli <c>IT-</c>.
///
/// <para>Trovato il 22 settembre 2026 dal campo: due chief nominati il giorno prima (uno con
/// <c>LIPP-CH</c>+<c>HPM</c>, l'altro con <c>LIBB-CH</c>+<c>LIRR-CHA1</c>, letti dal vivo su
/// <c>/v2/users/{vid}</c>) si erano loggati e non comparivano in Diagnostica. Il <see cref="RoleResolver"/> li
/// faceva Redattori; il roster li scartava perché il loro codice non comincia con <c>IT-</c>.</para>
/// </summary>
public class StaffRosterChiefAccTests
{
    private sealed class RosterRegistra : IStaffRosterRepository
    {
        public readonly Dictionary<int, IReadOnlyList<string>> Scritti = new();
        public readonly List<int> Disattivati = new();
        public List<int> Vid = new();

        public Task UpsertLoginAsync(int userId, string? displayName, IReadOnlyList<string> positions, CancellationToken ct = default)
        { Scritti[userId] = positions; return Task.CompletedTask; }
        public Task UpdateVerifiedAsync(int userId, string? displayName, string? atcRating, IReadOnlyList<string> positions, CancellationToken ct = default)
        { Scritti[userId] = positions; return Task.CompletedTask; }
        public Task DeactivateAsync(int userId, CancellationToken ct = default)
        { Disattivati.Add(userId); return Task.CompletedTask; }
        public Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(Vid);

        public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StaffRosterEntry>>(Array.Empty<StaffRosterEntry>());
        public Task<StaffRosterEntry?> FindAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<StaffRosterEntry?>(null);
        public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
    }

    private sealed class ElencoFinto : IUserDirectory
    {
        public readonly Dictionary<int, SourceUserStaff> Profili = new();
        public Task<SourceUserStaff?> GetUserAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult(Profili.TryGetValue(userId, out var p) ? p : null);
    }

    private static StaffRosterService Servizio(RosterRegistra repo, ElencoFinto? elenco = null) =>
        new(repo, elenco ?? new ElencoFinto(), Options.Create(new DivisionOptions()));

    [Theory]
    [InlineData("IT-AOA1", true)]
    [InlineData("IT-DIR", true)]
    [InlineData("LIPP-CH", true)]
    [InlineData("LIBB-CH", true)]
    [InlineData("LIRR-CHA1", true)]
    [InlineData("limm-ach", true)]
    [InlineData("HPM", false)]
    [InlineData("DE-DIR", false)]
    [InlineData("EDGG-CH", false)]
    [InlineData("IT-", false)]
    [InlineData("LI-CH", false)]
    public void Codice_della_divisione(string codice, bool atteso) =>
        Assert.Equal(atteso, StaffRosterService.CodiceDellaDivisione(new DivisionOptions()).IsMatch(codice));

    [Fact]
    public async Task Un_chief_d_ACC_entra_nel_roster_al_login_senza_i_codici_di_fuori()
    {
        var repo = new RosterRegistra();

        await Servizio(repo).RecordLoginAsync(new CurrentUser(100001, "Chief LIPP", "LIPP", new[] { "HPM", "LIPP-CH" }));

        Assert.Equal(new[] { "LIPP-CH" }, repo.Scritti[100001]);
    }

    [Fact]
    public async Task Chi_ha_solo_codici_di_fuori_resta_fuori()
    {
        var repo = new RosterRegistra();

        await Servizio(repo).RecordLoginAsync(new CurrentUser(1, "HQ", null, new[] { "HPM", "DE-DIR" }));

        Assert.Empty(repo.Scritti);
    }

    [Fact]
    public async Task La_verifica_giornaliera_non_disattiva_un_chief_d_ACC()
    {
        var repo = new RosterRegistra { Vid = { 100002 } };
        var elenco = new ElencoFinto();
        elenco.Profili[100002] = new SourceUserStaff(100002, null, "APC", "IT", true, new[] { "LIBB-CH", "LIRR-CHA1" });

        var disattivati = await Servizio(repo, elenco).VerifyAllAsync();

        Assert.Equal(0, disattivati);
        Assert.Empty(repo.Disattivati);
        Assert.Equal(new[] { "LIBB-CH", "LIRR-CHA1" }, repo.Scritti[100002]);
    }
}
