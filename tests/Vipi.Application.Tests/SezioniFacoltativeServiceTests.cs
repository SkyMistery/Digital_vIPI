using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>S133, carta 2026-10-28-stato-dei-documenti §1 D4: le sezioni facoltative le decide un admin, e solo fra
/// quelle che la regola conta.</summary>
public class SezioniFacoltativeServiceTests
{
    private sealed class Authz(VipiRole ruolo) : IEditAuthorizationService
    {
        public VipiRole Role { get; } = ruolo;
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 7;
        public string? CurrentName => "Prova";
    }

    private sealed class Store : ISezioniFacoltativeStore
    {
        public HashSet<(SectionProfile, string)> Righe { get; } = new();

        public Task<IReadOnlySet<(SectionProfile Profilo, string Chiave)>> TutteAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<(SectionProfile Profilo, string Chiave)>>(Righe.ToHashSet());

        public Task ImpostaAsync(SectionProfile profilo, string chiave, bool facoltativa, int userId, CancellationToken ct = default)
        {
            if (facoltativa) Righe.Add((profilo, chiave)); else Righe.Remove((profilo, chiave));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Un_admin_spegne_e_riaccende_una_sezione()
    {
        var store = new Store();
        var svc = new SezioniFacoltativeService(store, new Authz(VipiRole.Admin));

        await svc.ImpostaAsync(SectionProfile.App, "vfr", facoltativa: true);
        Assert.True(Assert.Single(await svc.ElencoAsync(), r => r.Profilo == SectionProfile.App && r.Chiave == "vfr").Facoltativa);

        await svc.ImpostaAsync(SectionProfile.App, "vfr", facoltativa: false);
        Assert.Empty(store.Righe);
    }

    [Fact]
    public async Task Un_editor_non_decide()
    {
        var svc = new SezioniFacoltativeService(new Store(), new Authz(VipiRole.Editor));

        await Assert.ThrowsAnyAsync<EditNotAllowedException>(() => svc.ImpostaAsync(SectionProfile.App, "vfr", true));
        await Assert.ThrowsAnyAsync<EditNotAllowedException>(() => svc.ElencoAsync());
    }

    /// <summary>Una sezione che la regola non conta (qui l'AOR, derivata) non ha interruttore.</summary>
    [Fact]
    public async Task Una_sezione_che_non_si_conta_non_si_spegne()
    {
        var svc = new SezioniFacoltativeService(new Store(), new Authz(VipiRole.Admin));

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => svc.ImpostaAsync(SectionProfile.App, "aor", true));
    }
}
