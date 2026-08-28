using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Promuovere e declassare. Carta <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §5.
///
/// <para>⚠️ <b>Le tre cose che si provano qui sono tre modi di perdere il controllo del prodotto</b>:
/// declassare sé stessi (ci si chiude fuori, e non si rimedia da dentro perché per assegnare permessi
/// bisogna essere admin), toccare un fondatore (la porta di servizio che esiste apposta per quel caso), e
/// «declassare» qualcuno sotto il livello che la sua posizione staff gli garantisce — che il <c>max</c>
/// renderebbe un <b>no-op silenzioso</b>, cioè far credere di aver tolto un permesso che c'è ancora.</para>
/// </summary>
public class RoleAdminServiceTests
{
    private const int Admin = 111;
    private const int Fondatore = 704798;
    private const int Tizio = 555;

    private static (RoleAdminService Servizio, DepositoFinto Deposito, CacheFinta Cache) Costruisci(
        VipiRole livelloAttore = VipiRole.Admin,
        int attore = Admin,
        params (int Vid, string[] Posizioni)[] roster)
    {
        var deposito = new DepositoFinto();
        var cache = new CacheFinta();
        var auth = new AuthOptions { FounderVids = { Fondatore } };
        var resolver = new RoleResolver(auth, new DivisionOptions());
        var authz = new AuthzFinta(livelloAttore, attore);

        return (new RoleAdminService(new RosterFinto(roster), deposito, cache, resolver, authz), deposito, cache);
    }

    // ------------------------------------------------------------------ l'elenco

    [Fact]
    public async Task Lelenco_mette_insieme_pavimento_e_promozione()
    {
        var (s, deposito, _) = Costruisci(roster: (Tizio, new[] { "IT-T01" }));
        deposito.Righe[Tizio] = VipiRole.Editor;

        var r = Assert.Single(await s.ListAsync());

        Assert.Equal(VipiRole.DivisionStaff, r.Floor);      // glielo dà IT-T01
        Assert.Equal(VipiRole.Editor, r.Override);          // gliel'abbiamo dato noi
        Assert.Equal(VipiRole.Editor, r.Effective);         // e vale il maggiore
    }

    /// <summary>
    /// ⚠️ Chi ha una promozione ma non è nel roster deve <b>comparire</b>: è il socio qualunque promosso a
    /// mano, cioè il caso per cui la promozione esiste. Saltarlo significherebbe che la pagina dei permessi
    /// non mostra un permesso che ha dato lei.
    /// </summary>
    [Fact]
    public async Task Chi_e_promosso_ma_non_e_staff_compare_lo_stesso()
    {
        var (s, deposito, _) = Costruisci(roster: (Tizio, new[] { "IT-T01" }));
        deposito.Righe[999] = VipiRole.Editor;

        var righe = await s.ListAsync();

        var socio = Assert.Single(righe, r => r.UserId == 999);
        Assert.Equal(VipiRole.User, socio.Floor);
        Assert.Equal(VipiRole.Editor, socio.Effective);
        Assert.True(socio.SoloPromossa);
    }

    [Fact]
    public async Task Lelenco_e_solo_per_gli_admin()
    {
        var (s, _, _) = Costruisci(livelloAttore: VipiRole.Editor);
        await Assert.ThrowsAsync<EditNotAllowedException>(() => s.ListAsync());
    }

    // ------------------------------------------------------------------ le tre guardie

    [Fact]
    public async Task Non_si_declassa_se_stessi()
    {
        var (s, deposito, _) = Costruisci(roster: (Admin, new[] { "IT-DIR" }));

        await Assert.ThrowsAsync<Aor.ValidationException>(() => s.SetAsync(Admin, VipiRole.User, null));
        await Assert.ThrowsAsync<Aor.ValidationException>(() => s.RemoveAsync(Admin));
        Assert.Empty(deposito.Righe);
    }

    [Fact]
    public async Task Un_fondatore_non_si_tocca()
    {
        var (s, deposito, _) = Costruisci();

        await Assert.ThrowsAsync<Aor.ValidationException>(() => s.SetAsync(Fondatore, VipiRole.User, null));
        Assert.Empty(deposito.Righe);
    }

    /// <summary>
    /// Sotto il pavimento il <c>max</c> lo renderebbe inerte: qui si rifiuta, perché un comando che accetta
    /// e non fa niente è peggio di un comando che dice di no.
    /// </summary>
    [Fact]
    public async Task Sotto_il_pavimento_si_rifiuta_invece_di_non_fare_niente()
    {
        var (s, deposito, _) = Costruisci(roster: (Tizio, new[] { "LIRR-CH" }));   // pavimento: Editor

        await Assert.ThrowsAsync<Aor.ValidationException>(() => s.SetAsync(Tizio, VipiRole.DivisionStaff, null));
        Assert.Empty(deposito.Righe);
    }

    [Fact]
    public async Task Sopra_il_pavimento_si_scrive()
    {
        var (s, deposito, _) = Costruisci(roster: (Tizio, new[] { "IT-T01" }));

        await s.SetAsync(Tizio, VipiRole.Editor, "aiuta su Milano");

        Assert.Equal(VipiRole.Editor, deposito.Righe[Tizio]);
    }

    [Fact]
    public async Task Un_vid_non_valido_non_si_promuove()
    {
        var (s, deposito, _) = Costruisci();
        await Assert.ThrowsAsync<Aor.ValidationException>(() => s.SetAsync(0, VipiRole.Editor, null));
        Assert.Empty(deposito.Righe);
    }

    [Fact]
    public async Task Promuovere_e_declassare_e_cosa_da_admin()
    {
        var (s, _, _) = Costruisci(livelloAttore: VipiRole.Editor);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => s.SetAsync(Tizio, VipiRole.Admin, null));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => s.RemoveAsync(Tizio));
    }

    // ------------------------------------------------------------------ la cache

    /// <summary>
    /// ⚠️ <b>Senza la ricarica la promozione non farebbe effetto fino al riavvio</b>: il livello si legge da
    /// un fotogramma in memoria, e chi scrive è l'unico che sa che è cambiato.
    /// </summary>
    [Fact]
    public async Task Ogni_scrittura_ricarica_il_fotogramma()
    {
        var (s, _, cache) = Costruisci(roster: (Tizio, new[] { "IT-T01" }));

        await s.SetAsync(Tizio, VipiRole.Editor, null);
        Assert.Equal(1, cache.Ricariche);

        await s.RemoveAsync(Tizio);
        Assert.Equal(2, cache.Ricariche);
    }

    // ------------------------------------------------------------------ doppi

    private sealed class DepositoFinto : IRoleOverrideStore
    {
        public readonly Dictionary<int, VipiRole> Righe = new();

        public Task<IReadOnlyList<RoleOverrideRow>> ListAsync(CancellationToken ct = default)
        {
            IReadOnlyList<RoleOverrideRow> righe = Righe
                .Select(r => new RoleOverrideRow(r.Key, r.Value, Admin, DateTime.UtcNow, null, null))
                .ToList();
            return Task.FromResult(righe);
        }

        public Task SetAsync(int userId, VipiRole level, int grantedByUserId, string? displayName, string? note, CancellationToken ct = default)
        {
            Righe[userId] = level;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(int userId, int actorUserId, CancellationToken ct = default) =>
            Task.FromResult(Righe.Remove(userId));
    }

    private sealed class CacheFinta : IRoleOverrides
    {
        public int Ricariche;
        public bool Loaded => true;
        public VipiRole? For(int userId) => null;
        public IReadOnlyDictionary<int, VipiRole> All { get; } = new Dictionary<int, VipiRole>();
        public Task ReloadAsync(CancellationToken ct = default) { Ricariche++; return Task.CompletedTask; }
    }

    private sealed class RosterFinto : IStaffRosterRepository
    {
        private readonly (int Vid, string[] Posizioni)[] _righe;
        public RosterFinto((int Vid, string[] Posizioni)[] righe) => _righe = righe;

        public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StaffRosterEntry>>(_righe
                .Select(r => new StaffRosterEntry(r.Vid, $"Tizio {r.Vid}", null, r.Posizioni, DateTime.UtcNow))
                .ToList());

        public Task<StaffRosterEntry?> FindAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult(_righe.Where(r => r.Vid == userId)
                .Select(r => new StaffRosterEntry(r.Vid, $"Tizio {r.Vid}", null, r.Posizioni, DateTime.UtcNow))
                .FirstOrDefault());

        public Task UpsertLoginAsync(int userId, string? displayName, IReadOnlyList<string> positions, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task UpdateVerifiedAsync(int userId, string? displayName, string? atcRating, IReadOnlyList<string> positions, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
    }

    private sealed class AuthzFinta : IEditAuthorizationService
    {
        public AuthzFinta(VipiRole livello, int io) { Role = livello; CurrentUserId = io; }

        public VipiRole Role { get; }
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId { get; }
        public string? CurrentName => "Chi comanda";
        public void EnsureAdmin() { if (!IsAdmin) throw new EditNotAllowedException(); }
    }
}
