using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <b>I dati di un aeroporto si scrivono col lock del suo documento</b> (carta
/// <c>docs/feature/2026-09-04-aeroporto-porta-sola.md</c>).
///
/// <para>Fino al 4 settembre 2026 piste, SID, quote e frequenze di uno scalo si scrivevano <b>senza alcun
/// lock</b>: bastava il ruolo sulla ACC. Due persone potevano lavorare sullo stesso scalo senza vedersi, e
/// l'ultima che salvava vinceva.</para>
///
/// <para>⚠️ <b>Perché nel servizio e non nel bottone.</b> È la stessa regola già pagata su
/// <c>/services/vsop/versions</c> il 21 agosto 2026: un tasto spento non è una guardia — l'editor è una
/// fotografia, e chi arriva da un'altra scheda passerebbe lo stesso.</para>
///
/// <para>⚠️ E il re-import ha una regola <b>diversa</b>, non più debole per distrazione: non chiede il lock
/// (la pagina degli aeroporti lo lancia su N scali e quel lock non ce l'ha), chiede che non ce l'abbia un
/// altro.</para>
///
/// <para>Repo veri su SQLite in-memory: il lock è una scrittura atomica DB-side, e un finto direbbe soltanto
/// ciò che gli si insegna.</para>
/// </summary>
public class AirportLockGuardTests : IAsyncLifetime
{
    private const int Io = 7;
    private const int Altri = 99;

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditingRepository _editing = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));

        var acc = new Acc { Code = "LIPP", Name = "Padova" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIPZ", Name = "Venezia", Acc = acc });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private AirportEditingService Servizio(int comeChi)
    {
        var authz = new AuthzFinta(comeChi);
        var repo = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        return new AirportEditingService(repo, authz, new NienteDirectory(), new NienteDetails(),
            new EfImportPolicyStore(_db), new AirportLockGuard(repo, _editing, authz));
    }

    /// <summary>Il documento dello scalo: lo crea l'apertura dell'editor, e senza non c'è lock da prendere.</summary>
    private Task<int> ApriEditorAsync() => Servizio(Io).EnsureDocumentAsync("LIPZ");

    private Task LockA(int documentId, int userId) =>
        _editing.AcquireOrInspectLockAsync(documentId, userId, $"VID {userId}", 30);

    /// <summary>La scrittura di prova: i livelli di transizione. ⚠️ Non le piste, che la policy d'import
    /// dichiara «di sorgente» e quindi rifiuta per un'altra ragione — la prova direbbe verde per il motivo
    /// sbagliato.</summary>
    private static IReadOnlyList<TlRow> UnLivello() => new[] { new TlRow(0, 1013, null, "FL70") };

    /// <summary>Il livello scritto dalla prova c'è in archivio? ⚠️ Non «la tabella è vuota»: aprire l'editor
    /// semina quattro livelli di partenza (TA + 1000/1500/2000/2500 ft), e una prova che li conta direbbe di
    /// sì per il motivo sbagliato.</summary>
    private async Task<bool> IlLivelloDiProvaCeAsync() =>
        await _db.AirportTransitionLevels.AsNoTracking().AnyAsync(t => t.Level == "FL70");

    // ---- Le scritture editoriali: il lock dev'essere MIO ----------------------------------------------

    [Fact]
    public async Task SenzaLock_LaScritturaNonPassa()
    {
        await ApriEditorAsync();

        await Assert.ThrowsAsync<EditConflictException>(
            () => Servizio(Io).SaveTransitionLevelsAsync("LIPZ", UnLivello()));

        Assert.False(await IlLivelloDiProvaCeAsync());
    }

    [Fact]
    public async Task ColLockDiUnAltro_LaScritturaNonPassa_EIlMessaggioDiceChi()
    {
        var doc = await ApriEditorAsync();
        await LockA(doc, Altri);

        var ex = await Assert.ThrowsAsync<EditConflictException>(
            () => Servizio(Io).SaveTransitionLevelsAsync("LIPZ", UnLivello()));

        Assert.Contains("VID 99", ex.Message);
        Assert.False(await IlLivelloDiProvaCeAsync());
    }

    [Fact]
    public async Task ColMioLock_LaScritturaPassa()
    {
        var doc = await ApriEditorAsync();
        await LockA(doc, Io);

        await Servizio(Io).SaveTransitionLevelsAsync("LIPZ", UnLivello());

        Assert.True(await IlLivelloDiProvaCeAsync());
    }

    /// <summary>⚠️ Un lock SCADUTO non è un lock — ma non è nemmeno il mio: chi ha lasciato scadere il suo
    /// deve ripremere «Modifica», che è ciò che l'editor fa da sé riacquisendolo.</summary>
    [Fact]
    public async Task LockScaduto_LaScritturaNonPassa()
    {
        var doc = await ApriEditorAsync();
        await LockA(doc, Io);
        var d = await _db.Documents.FirstAsync(x => x.Id == doc);
        d.LockExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<EditConflictException>(
            () => Servizio(Io).SaveTransitionLevelsAsync("LIPZ", UnLivello()));
    }

    /// <summary>Le altre sei scritture editoriali passano dalla stessa porta: se una si scollegasse, il lock
    /// varrebbe per le piste e non per le SID — cioè non varrebbe.</summary>
    [Fact]
    public async Task TutteLeScrittureEditorialiChiedonoIlLock()
    {
        await ApriEditorAsync();
        var s = Servizio(Io);

        await Assert.ThrowsAsync<EditConflictException>(() => s.SetTransitionAltitudeAsync("LIPZ", 6000));
        await Assert.ThrowsAsync<EditConflictException>(() => s.SaveRunwaysAsync("LIPZ", Array.Empty<RunwayRow>()));
        await Assert.ThrowsAsync<EditConflictException>(() => s.SaveTransitionLevelsAsync("LIPZ", UnLivello()));
        await Assert.ThrowsAsync<EditConflictException>(() => s.SaveRunwayRulesAsync("LIPZ", Array.Empty<RunwayRuleRow>()));
        await Assert.ThrowsAsync<EditConflictException>(() => s.SaveSidsAsync("LIPZ", Array.Empty<SidRow>()));
        await Assert.ThrowsAsync<EditConflictException>(() => s.SaveFrequencyLinksAsync("LIPZ", Array.Empty<int>()));
        await Assert.ThrowsAsync<EditConflictException>(() => s.UpdateImportedSidAsync("LIPZ", 1, null, false, null, null, false, null, null, null));
    }

    // ---- Il re-import: basta che il lock non sia di un ALTRO ------------------------------------------

    [Fact]
    public async Task IlReimport_PassaSenzaLock()
    {
        await ApriEditorAsync();

        await Servizio(Io).ReimportFromSourceAsync("LIPZ");   // niente eccezione: è il caso della pagina admin
    }

    [Fact]
    public async Task IlReimport_NonPassaSopraAlLockDiUnAltro()
    {
        var doc = await ApriEditorAsync();
        await LockA(doc, Altri);

        var ex = await Assert.ThrowsAsync<EditConflictException>(
            () => Servizio(Io).ReimportFromSourceAsync("LIPZ"));

        Assert.Contains("VID 99", ex.Message);
    }

    // ---- I doppi -------------------------------------------------------------------------------------

    /// <summary>Autorizzazione permissiva: il gate ACC è provato altrove, qui interessa il solo lock.</summary>
    private sealed class AuthzFinta : IEditAuthorizationService
    {
        public AuthzFinta(int userId) { CurrentUserId = userId; }

        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId { get; }
        public string? CurrentName => $"VID {CurrentUserId}";
        public void EnsureAdmin() { }
    }

    /// <summary>La sorgente esterna non serve a queste regole. Due finti vuoti valgono più di un mock, che
    /// qui direbbe soltanto che non è stato chiamato.</summary>
    private sealed class NienteDirectory : IAirportDirectory
    {
        public Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceAirport>>(Array.Empty<SourceAirport>());
        public Task<SourceAirport?> GetByIcaoAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<SourceAirport?>(null);
    }

    private sealed class NienteDetails : IAirportDetailProvider
    {
        public Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceAtcPosition>>(Array.Empty<SourceAtcPosition>());
        public Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default) =>
            Task.FromResult<SourceAtcPosition?>(null);
        public Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceRunway>>(Array.Empty<SourceRunway>());
    }
}
