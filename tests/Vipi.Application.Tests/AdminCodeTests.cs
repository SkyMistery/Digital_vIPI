using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;

namespace Vipi.Application.Tests;

/// <summary>Quali staff position contano come admin editing (IsAdmin). Vertici divisione + AOC/AOAC/AOA&lt;n&gt;.</summary>
public class AdminCodeTests
{
    [Theory]
    [InlineData("IT-DIR", true)]
    [InlineData("IT-ADIR", true)]
    [InlineData("IT-AOC", true)]
    [InlineData("IT-AOAC", true)]
    [InlineData("IT-AOA1", true)]
    [InlineData("IT-AOA12", true)]
    [InlineData("IT-WM", true)]
    [InlineData("IT-AWM", true)]
    [InlineData("it-dir", true)]          // case-insensitive
    [InlineData("IT-CH", false)]          // CH da solo non è admin
    [InlineData("IT-AOA", false)]         // serve il numero
    [InlineData("IT-DIRX", false)]
    [InlineData("", false)]
    public void IsAdmin_riconosce_i_codici_corretti(string staffCode, bool expected)
    {
        var user = new CurrentUser(123, "Tester", "LIRR", new[] { staffCode });
        var authz = new EditAuthorizationService(
            new FakeUser(user), new FakeGrants(),
            Options.Create(new AuthOptions()), Options.Create(new DivisionOptions()));

        Assert.Equal(expected, authz.IsAdmin);
    }

    [Fact]
    public void Config_override_sostituisce_i_default()
    {
        var opt = Options.Create(new AuthOptions { AdminStaffCodes = { @"^IT-TA\d+$" } });

        bool IsAdmin(string code) => new EditAuthorizationService(
            new FakeUser(new CurrentUser(1, "T", "LIRR", new[] { code })), new FakeGrants(),
            opt, Options.Create(new DivisionOptions())).IsAdmin;

        Assert.True(IsAdmin("IT-TA1"));    // nuovo codice da config
        Assert.False(IsAdmin("IT-DIR"));   // i default non valgono più quando la config è popolata
    }

    [Fact]
    public void Cambiare_Division_Code_sposta_i_codici_admin()
    {
        var de = Options.Create(new DivisionOptions { Code = "DE" });

        bool IsAdmin(string code) => new EditAuthorizationService(
            new FakeUser(new CurrentUser(1, "T", "EDGG", new[] { code })), new FakeGrants(),
            Options.Create(new AuthOptions()), de).IsAdmin;

        Assert.True(IsAdmin("DE-DIR"));    // admin nella nuova divisione
        Assert.True(IsAdmin("DE-AOA3"));
        Assert.False(IsAdmin("IT-DIR"));   // la vecchia divisione non è più admin
    }

    /// <summary>
    /// L'identità si risolve <b>una volta per scope</b>. Non è ottimizzazione fine a sé stessa: ogni
    /// <c>Get()</c> rilegge i claim e rifà il parse dell'array JSON <c>userStaffPositions</c>, e le pagine
    /// leggono <c>IsAdmin</c> dentro il markup — <c>StrutturaPage</c> sette volte per render, una delle
    /// quali dentro il <c>foreach</c> sui nodi della gerarchia. Su ~300 callsign erano ~300 parse a ogni
    /// ridisegno, per rispondere sempre la stessa cosa.
    /// </summary>
    [Fact]
    public void L_identita_si_chiede_una_volta_sola_per_scope()
    {
        var provider = new FakeUserContatore(new CurrentUser(123, "Tester", "LIRR", new[] { "IT-AOC" }));
        var authz = new EditAuthorizationService(provider, new FakeGrants(),
            Options.Create(new AuthOptions()), Options.Create(new DivisionOptions()));

        for (var i = 0; i < 50; i++)
        {
            _ = authz.IsAdmin;
            _ = authz.CurrentUserId;
            _ = authz.CurrentName;
        }

        Assert.Equal(1, provider.Letture);
        Assert.True(authz.IsAdmin);
        Assert.Equal(123, authz.CurrentUserId);
    }

    /// <summary>
    /// L'anonimo è il caso che si sbaglia per primo memoizzando: senza distinguere «non ancora chiesto» da
    /// «chiesto, e non c'è nessuno», un <c>null</c> in cache sembra un valore mancante e il giro si rifà
    /// ogni volta — cioè proprio il caso peggiore, perché le pagine pubbliche chiedono <c>IsAdmin</c> per
    /// decidere se mostrare i comandi di editing.
    /// </summary>
    [Fact]
    public void Anche_l_anonimo_si_chiede_una_volta_sola()
    {
        var provider = new FakeUserContatore(null);
        var authz = new EditAuthorizationService(provider, new FakeGrants(),
            Options.Create(new AuthOptions()), Options.Create(new DivisionOptions()));

        for (var i = 0; i < 50; i++) _ = authz.IsAdmin;

        Assert.Equal(1, provider.Letture);
        Assert.False(authz.IsAdmin);
        Assert.Null(authz.CurrentUserId);
    }

    private sealed class FakeUserContatore : ICurrentUserProvider
    {
        private readonly CurrentUser? _u;
        public FakeUserContatore(CurrentUser? u) => _u = u;
        public int Letture { get; private set; }
        public CurrentUser? Get() { Letture++; return _u; }
    }

    private sealed class FakeUser : ICurrentUserProvider
    {
        private readonly CurrentUser _u;
        public FakeUser(CurrentUser u) => _u = u;
        public CurrentUser? Get() => _u;
    }

    // IsAdmin non tocca i grant: stub inerte.
    private sealed class FakeGrants : IEditGrantRepository
    {
        public Task<IReadOnlyList<GrantRow>> ListAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> AddAsync(int UserId, string? displayName, string accCode, int GrantedByUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RevokeAsync(int grantId, int actorUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> HasGrantAsync(int UserId, string accCode, CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GetDocumentAccCodeAsync(int documentId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
