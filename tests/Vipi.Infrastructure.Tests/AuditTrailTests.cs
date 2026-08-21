using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Cosa finisce davvero nel registro di audit (carta <c>2026-08-22-audit-cosa-registra.md</c>).
/// Ogni atto amministrativo che non si può disfare deve lasciare <b>una</b> riga, con l'attore giusto.
/// </summary>
public class AuditTrailTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? User { get; set; }
        public CurrentUser? Get() => User;
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static CurrentUser Admin(int userId) => new(userId, $"Admin{userId}", "LIRR", new[] { "IT-AOC" });

    private EditAuthorizationService Authz(CurrentUser user) =>
        new(new FakeUser { User = user }, new EfEditGrantRepository(_db),
            Microsoft.Extensions.Options.Options.Create(new AuthOptions()),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.DivisionOptions()));

    /// <summary>
    /// Il difetto storico: la revoca scriveva <c>g.GrantedByUserId</c>, cioè chi aveva <b>concesso</b>. Con due
    /// admin diversi (uno concede, l'altro revoca) il registro attribuiva l'atto alla persona sbagliata — ed è
    /// esattamente il caso in cui a qualcuno interessa saperlo.
    /// </summary>
    [Fact]
    public async Task Revoca_Permesso_Registra_Chi_Revoca_Non_Chi_Aveva_Concesso()
    {
        var grantId = await Authz(Admin(101)).AddGrantAsync(555, "Mario Rossi", "LIRR");
        await Authz(Admin(202)).RevokeGrantAsync(grantId);

        var riga = await _db.AuditLogs.Where(a => a.EntityType == "EditGrant" && a.Action == AuditAction.Delete)
            .SingleAsync();
        Assert.Equal(202, riga.UserId);                       // chi revoca
        Assert.Equal(grantId.ToString(), riga.EntityId);
        Assert.Contains("\"UserId\":555", riga.DetailsJson);  // su chi
        Assert.Contains("LIRR", riga.DetailsJson);
    }

    /// <summary>La concessione continua a registrare chi concede: la correzione non l'ha spostata.</summary>
    [Fact]
    public async Task Concessione_Registra_Chi_Concede()
    {
        await Authz(Admin(101)).AddGrantAsync(555, "Mario Rossi", "LIRR");

        var riga = await _db.AuditLogs.Where(a => a.EntityType == "EditGrant" && a.Action == AuditAction.Create)
            .SingleAsync();
        Assert.Equal(101, riga.UserId);
    }
}
