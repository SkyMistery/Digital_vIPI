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
        public Task<int> AddAsync(int UserId, string? displayName, string firCode, int GrantedByUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RevokeAsync(int grantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> HasGrantAsync(int UserId, string firCode, CancellationToken ct = default) => Task.FromResult(false);
        public Task<string?> GetDocumentFirCodeAsync(int documentId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
