using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
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

    /// <summary>
    /// L'atto meno reversibile dell'applicazione non lasciava traccia: <c>DeleteAsync</c> porta via versioni,
    /// sezioni, blocchi e release e fino al 22 agosto 2026 non scriveva una riga. E il titolo dev'esserci: un
    /// registro che dice «eliminato il documento 7» non distingue una pulizia da un incidente.
    /// </summary>
    [Fact]
    public async Task Eliminazione_Documento_Lascia_Riga_Col_Titolo()
    {
        var repo = TestReleaseTargets.AdminRepo(_db);
        var doc = (await repo.ListAsync()).First(d => d.DocumentId is not null);
        var riferimento = new ManagedDocRef(doc.Kind, doc.ReleaseKey, doc.DocumentId);

        await repo.DeleteAsync(riferimento, actorUserId: 404);

        var riga = await _db.AuditLogs.Where(a => a.EntityType == "Document" && a.Action == AuditAction.Delete)
            .SingleAsync();
        Assert.Equal(404, riga.UserId);
        Assert.Equal(doc.DocumentId!.Value.ToString(), riga.EntityId);
        Assert.Contains(doc.Title, riga.DetailsJson);
        Assert.False(await _db.Documents.AnyAsync(d => d.Id == doc.DocumentId));   // la riga sopravvive al documento
    }

    /// <summary>Nascondere cambia la visibilità pubblica: è un atto, e va nel registro con il verso del cambio.</summary>
    [Fact]
    public async Task Nascondi_E_Rimostra_Lasciano_Una_Riga_Ciascuno()
    {
        var repo = TestReleaseTargets.AdminRepo(_db);
        var doc = (await repo.ListAsync()).First(d => d.DocumentId is not null && !d.IsHidden);
        var riferimento = new ManagedDocRef(doc.Kind, doc.ReleaseKey, doc.DocumentId);

        await repo.SetHiddenAsync(riferimento, hidden: true, actorUserId: 303);
        await repo.SetHiddenAsync(riferimento, hidden: false, actorUserId: 303);

        var righe = await _db.AuditLogs.Where(a => a.EntityType == "Document" && a.Action == AuditAction.Update)
            .OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, righe.Count);
        Assert.Contains("\"Hidden\":true", righe[0].DetailsJson);
        Assert.Contains("\"Hidden\":false", righe[1].DetailsJson);
    }

    /// <summary>
    /// Il non-evento non si scrive. Un registro che cresce per sempre non si riempie di righe che dicono
    /// «nascosto un documento già nascosto»: la seconda chiamata non cambia niente, quindi non è un atto.
    /// </summary>
    [Fact]
    public async Task Nascondere_Cio_Che_E_Gia_Nascosto_Non_Scrive_Niente()
    {
        var repo = TestReleaseTargets.AdminRepo(_db);
        var doc = (await repo.ListAsync()).First(d => d.DocumentId is not null && !d.IsHidden);
        var riferimento = new ManagedDocRef(doc.Kind, doc.ReleaseKey, doc.DocumentId);

        await repo.SetHiddenAsync(riferimento, hidden: true, actorUserId: 303);
        await repo.SetHiddenAsync(riferimento, hidden: true, actorUserId: 303);

        Assert.Equal(1, await _db.AuditLogs.CountAsync(a => a.EntityType == "Document"));
    }
}
