using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
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
        new(new FakeUser { User = user },
            new Vipi.Application.Auth.RoleResolver(new Vipi.Application.Auth.AuthOptions(), new Vipi.Application.DivisionOptions()), SenzaPromozioni.Instance);

    private EfRoleOverrideStore Promozioni() => new(_db);

    /// <summary>
    /// ⚠️ Il difetto storico stava sulle concessioni, morte il 28 agosto 2026, ma <b>l'invariante è la
    /// stessa e va tenuta ferma dove il permesso vive adesso</b>: la revoca scriveva chi aveva
    /// <b>concesso</b> invece di chi revocava, e con due admin diversi il registro attribuiva l'atto alla
    /// persona sbagliata — esattamente il caso in cui a qualcuno interessa saperlo.
    /// </summary>
    [Fact]
    public async Task Togliere_Una_Promozione_Registra_Chi_La_Toglie_Non_Chi_La_Aveva_Data()
    {
        await Promozioni().SetAsync(555, VipiRole.Editor, grantedByUserId: 101, "Mario Rossi", null);
        await Promozioni().RemoveAsync(555, actorUserId: 202);

        var riga = await _db.AuditLogs.Where(a => a.EntityType == "RoleOverride" && a.Action == AuditAction.Delete)
            .SingleAsync();
        Assert.Equal(202, riga.UserId);                       // chi toglie
        Assert.Equal("555", riga.EntityId);
        Assert.Contains("\"UserId\":555", riga.DetailsJson);  // su chi
        Assert.Contains("Editor", riga.DetailsJson);          // e da quale livello
    }

    /// <summary>La promozione registra chi la firma: è il permesso più alto che si possa dare a mano.</summary>
    [Fact]
    public async Task Una_Promozione_Registra_Chi_La_Firma()
    {
        await Promozioni().SetAsync(555, VipiRole.Admin, grantedByUserId: 101, "Mario Rossi", "aiuta in direzione");

        var riga = await _db.AuditLogs.Where(a => a.EntityType == "RoleOverride" && a.Action == AuditAction.Create)
            .SingleAsync();
        Assert.Equal(101, riga.UserId);
        Assert.Contains("Admin", riga.DetailsJson);
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

    /// <summary>
    /// Togliere il lock a un'altra persona è un atto d'autorità — esposto in /services/vsop/versions dal 21 agosto 2026 —
    /// e la riga serve solo se dice <b>a chi</b> è stato tolto.
    /// </summary>
    [Fact]
    public async Task ForceUnlock_Documento_Registra_Chi_Teneva_Il_Lock()
    {
        var repo = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var docId = await _db.Documents.Select(d => d.Id).FirstAsync();
        await repo.AcquireOrInspectLockAsync(docId, 555, "Giulia Bianchi", 30);

        await repo.ForceUnlockAsync(docId, actorUserId: 704798);

        var riga = await _db.AuditLogs.Where(a => a.Action == AuditAction.ForceUnlock).SingleAsync();
        Assert.Equal(704798, riga.UserId);
        Assert.Equal("Document", riga.EntityType);
        Assert.Contains("Giulia Bianchi", riga.DetailsJson);
        Assert.Contains("\"HeldByUserId\":555", riga.DetailsJson);
    }

    /// <summary>Stessa cosa per le pagine senza Document (struttura, newdoc), che usano EditResourceLock.</summary>
    [Fact]
    public async Task ForceUnlock_Risorsa_Registra_Chiave_E_Chi_Teneva()
    {
        var locks = new EfResourceLockRepository(_db);
        await locks.AcquireOrInspectAsync("structure", 555, "Giulia Bianchi", 3);

        await locks.ForceUnlockAsync("structure", actorUserId: 704798);

        var riga = await _db.AuditLogs.Where(a => a.Action == AuditAction.ForceUnlock).SingleAsync();
        Assert.Equal("EditResourceLock", riga.EntityType);
        Assert.Equal("structure", riga.EntityId);
        Assert.Contains("Giulia Bianchi", riga.DetailsJson);
    }

    /// <summary>Forzare un lock che non c'è non è un atto: nessuna riga.</summary>
    [Fact]
    public async Task ForceUnlock_Su_Lock_Libero_Non_Scrive_Niente()
    {
        var docId = await _db.Documents.Select(d => d.Id).FirstAsync();
        await new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db))
            .ForceUnlockAsync(docId, actorUserId: 704798);
        await new EfResourceLockRepository(_db).ForceUnlockAsync("structure", actorUserId: 704798);

        Assert.Equal(0, await _db.AuditLogs.CountAsync(a => a.Action == AuditAction.ForceUnlock));
    }
}
