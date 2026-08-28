using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La guardia del 21 agosto 2026: <see cref="DocumentAdminService"/> non nasconde né elimina un documento che
/// un'<b>altra</b> persona sta modificando.
///
/// <para><b>Perché sta nel servizio e non nella pagina.</b> Prima queste due chiamate guardavano solo il grant
/// ACC: si poteva eliminare un documento mentre qualcuno lo editava, e quella persona lo scopriva al salvataggio
/// con il lavoro già perso. Spegnere il tasto in /services/vsop/versions non basta — l'elenco è una fotografia, e chi
/// arriva da un'altra scheda o con la lista vecchia in mano passerebbe lo stesso.</para>
///
/// <para>Repo veri su SQLite in-memory: il lock è una scrittura atomica DB-side, e un finto direbbe solo ciò
/// che gli si insegna.</para>
/// </summary>
public class DocumentAdminLockGuardTests : IAsyncLifetime
{
    private const int Io = 7;
    private const int Altri = 99;

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditingRepository _editing = default!;
    private int _docId;
    private ManagedDocRef _doc = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        var doc = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.Sectors.Add(new Sector
        {
            Acc = acc, Callsign = "LIRR_ROOT_CTR", Name = "Roma root", Type = SectorType.Ctr,
            Kind = SectorKind.Acc, IsActive = true, DocumentId = doc.Id, IsPrimary = true,
        });
        await _db.SaveChangesAsync();

        _docId = doc.Id;
        _doc = new ManagedDocRef(ReleaseTargetType.AccVipi, "LIRR|LIRR_ROOT_CTR", _docId);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private DocumentAdminService Servizio(int comeChi) =>
        new(TestReleaseTargets.AdminRepo(_db), new AuthzFinta(comeChi), _editing);

    private Task LockA(int userId, int minuti = 30) =>
        _editing.AcquireOrInspectLockAsync(_docId, userId, $"VID {userId}", minuti);

    private Task<Document> RileggiAsync() => _db.Documents.AsNoTracking().FirstAsync(d => d.Id == _docId);

    [Fact]
    public async Task SenzaLock_SiNascondeESiElimina()
    {
        await Servizio(Io).SetHiddenAsync(_doc, hidden: true);
        Assert.True((await RileggiAsync()).IsHidden);

        await Servizio(Io).DeleteAsync(_doc);
        Assert.False(await _db.Documents.AnyAsync(d => d.Id == _docId));
    }

    [Fact]
    public async Task LockDiUnAltro_RifiutaNascondi()
    {
        await LockA(Altri);

        var ex = await Assert.ThrowsAsync<EditConflictException>(() => Servizio(Io).SetHiddenAsync(_doc, hidden: true));
        Assert.Contains("VID 99", ex.Message);
        Assert.False((await RileggiAsync()).IsHidden);
    }

    [Fact]
    public async Task LockDiUnAltro_RifiutaElimina()
    {
        await LockA(Altri);

        await Assert.ThrowsAsync<EditConflictException>(() => Servizio(Io).DeleteAsync(_doc));
        Assert.True(await _db.Documents.AnyAsync(d => d.Id == _docId));
    }

    /// <summary>Il proprio lock non è un ostacolo: chi sta editando può nascondere il proprio documento.</summary>
    [Fact]
    public async Task IlProprioLock_NonBlocca()
    {
        await LockA(Io);

        await Servizio(Io).SetHiddenAsync(_doc, hidden: true);
        Assert.True((await RileggiAsync()).IsHidden);
    }

    /// <summary>⚠️ Un lock SCADUTO non è un lock: il documento di chi ha chiuso la scheda non resta inchiodato.</summary>
    [Fact]
    public async Task LockScaduto_NonBlocca()
    {
        await LockA(Altri, minuti: 30);
        var d = await _db.Documents.FirstAsync(x => x.Id == _docId);
        d.LockExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        await Servizio(Io).DeleteAsync(_doc);
        Assert.False(await _db.Documents.AnyAsync(x => x.Id == _docId));
    }

    /// <summary>
    /// L'ordine dei due gate conta: chi non ha il permesso sull'ACC non deve scoprire dal messaggio d'errore
    /// <b>chi</b> sta lavorando su un documento che non può toccare.
    /// </summary>
    [Fact]
    public async Task SenzaPermesso_IlRifiutoNonParlaDelLock()
    {
        await LockA(Altri);

        var servizio = new DocumentAdminService(TestReleaseTargets.AdminRepo(_db), new AuthzFinta(Io, puo: false), _editing);
        await Assert.ThrowsAsync<EditNotAllowedException>(() => servizio.DeleteAsync(_doc));
    }

    /// <summary>Autorizzazione finta: il gate ACC è provato altrove, qui interessa solo il lock.</summary>
    private sealed class AuthzFinta : IEditAuthorizationService
    {
        private readonly bool _puo;
        public AuthzFinta(int userId, bool puo = true) { CurrentUserId = userId; _puo = puo; }

        public bool IsAdmin => _puo;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId { get; }
        public string? CurrentName => $"VID {CurrentUserId}";
        public void EnsureAdmin() { if (!_puo) throw new EditNotAllowedException(); }
    }
}
