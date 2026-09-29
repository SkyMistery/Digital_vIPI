using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// «Remotizza» (S50, committente, 29 settembre 2026): quando un APP non remotizzato passa sotto un ACC (Palermo sotto
/// Roma), il contenuto della sua vIPI APP si SPOSTA nella vIPI dell'ACC come gruppo APP, e lì resta. Prima andava
/// riscritto da capo.
/// </summary>
public class RemotizzazioneTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditingRepository _editing = default!;
    private EfAtcUnitRepository _enti = default!;
    private AccDocumentService _acc = default!;
    private RemotizzazioneService _servizio = default!;
    private int _appDoc;
    private int _unitId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        _editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        _enti = new EfAtcUnitRepository(_db);
        var authz = new Authz();
        _acc = new AccDocumentService(new EfAccDerivationRepository(_db), _editing, authz,
            TestReleaseTargets.ReleaseRepo(_db), LockConcesso.Instance);
        var admin = TestReleaseTargets.AdminRepo(_db);
        _servizio = Servizio(new EfDocumentProfileRepository(_db));

        // La vIPI APP di Pisa, col suo ente, e dentro del testo scritto a mano e un ordine delle frequenze.
        _appDoc = await _enti.EnsureDocumentAsync("LIRP_APP", "Pisa Approach", "LIRR", SectionProfile.App, 0);
        _unitId = (await _enti.FindAsync("LIRP_APP"))!.Id;
        var bozza = await _db.DocumentVersions.Where(v => v.DocumentId == _appDoc).Select(v => v.Id).SingleAsync();
        var tecnica = await _db.DocumentSections.SingleAsync(s => s.DocumentVersionId == bozza && s.SectionKey == SectionKeys.OperatingTechnique);
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = bozza, Section = tecnica, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Reduced,
            Visibility = BlockVisibility.Always, Body = "Vettoramento verso PISATOKEN", RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();
        await new EfDocumentProfileRepository(_db).SaveFreqOrderAsync(_appDoc,
            new[] { new AppFreqOrderOverride("LIRP_APP", 0) });
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private RemotizzazioneService Servizio(Vipi.Application.Abstractions.IDocumentProfileRepository profili)
    {
        var authz = new Authz();
        var admin = TestReleaseTargets.AdminRepo(_db);
        return new RemotizzazioneService(_enti, _editing, _acc, new EfAccDerivationRepository(_db), profili,
            new EfAppDerivationRepository(_db),
            new DocumentUnionService(new EfDocumentUnionRepository(_db), admin, authz, TestReleaseTargets.Registry(_db)),
            admin, LockConcesso.Instance, authz, new EfUnitOfWork(_db));
    }

    /// <summary>Pubblica la vIPI ACC e la mette in vigore adesso, come «Pubblica ora».</summary>
    private async Task PubblicaVipiAccAsync()
    {
        var modello = await _acc.LoadForEditAsync("LIRR");
        var bozza = await _db.DocumentVersions.Where(v => v.DocumentId == modello.DocumentId && v.Status == DocumentStatus.Draft)
            .Select(v => v.Id).FirstAsync();
        await _editing.PublishAsync(bozza, actorUserId: 1, note: null);
        var releases = TestReleaseTargets.ReleaseRepo(_db);
        var key = $"LIRR|{(await _acc.GetIdentityAsync("LIRR"))!.RootCallsign}";
        var snap = await releases.SnapshotWorkingAsync(ReleaseTargetType.AccVipi, key, "2607");
        await releases.SaveReleaseAsync(ReleaseTargetType.AccVipi, key, "2607", DateTime.UtcNow.AddMinutes(-1), snap!,
            createdByUserId: 1, note: null);
    }

    [Fact]
    public async Task Il_contenuto_della_vipi_app_si_sposta_nella_vipi_acc_come_gruppo_app()
    {
        var esito = await _servizio.RemotizzaAsync(_unitId);

        Assert.Equal("LIRR", esito.AccCode);
        var modello = await _acc.LoadForEditAsync("LIRR");
        var gruppo = Assert.Single(modello.Blocks, b => b.Block.Kind == AccBlockKind.AppGroup);
        Assert.Equal(esito.BlockSectionId, gruppo.BlockSectionId);
        Assert.Equal("Pisa Approach", gruppo.Block.Title);
        Assert.Equal(new[] { "LIRP_APP" }, gruppo.Block.MemberCallsigns);
        Assert.Equal(_unitId, gruppo.Block.UnitId);
        Assert.Contains(gruppo.Block.FreqOrder, o => o.Callsign == "LIRP_APP");

        // Il testo scritto a mano è venuto con la sua sezione, sotto il blocco.
        var testo = await _db.ContentBlocks.AsNoTracking().Include(b => b.Section)
            .SingleAsync(b => b.Body == "Vettoramento verso PISATOKEN"
                              && b.Section!.DocumentVersion!.DocumentId == esito.AccDocumentId);
        Assert.Equal(SectionKeys.OperatingTechnique, testo.Section!.SectionKey);
        Assert.Equal(esito.BlockSectionId, testo.Section.ParentSectionId);

        // S52 (scelta del committente): la vIPI APP resta pubblica finché la vIPI ACC col gruppo non va in vigore.
        Assert.Equal(AtcUnitMode.OwnDocument, (await _enti.FindAsync("LIRP_APP"))!.Mode);
        Assert.False((await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == _appDoc)).IsHidden);
        Assert.Equal("LIRR", await _servizio.SpostamentoInCorsoAsync(_unitId));
        Assert.Equal(0, await _servizio.ConcludiSpostamentiAsync());   // la vIPI ACC non è ancora in vigore

        await PubblicaVipiAccAsync();
        Assert.Equal(1, await _servizio.ConcludiSpostamentiAsync());

        // Ora l'ente vive nella vIPI dell'ACC; la sua vIPI APP è nascosta, e non ha più una porta come vIPI APP.
        Assert.Equal(AtcUnitMode.InAccVipi, (await _enti.FindAsync("LIRP_APP"))!.Mode);
        Assert.True((await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == _appDoc)).IsHidden);
        Assert.Null(await new EfAppDerivationRepository(_db).ResolveForDocumentAsync("LIRP_APP"));
        Assert.Null(await _servizio.SpostamentoInCorsoAsync(_unitId));
        Assert.Equal(0, await _servizio.ConcludiSpostamentiAsync());   // idempotente
    }

    /// <summary>
    /// Revisione (S52): «Sposta» è tutto o niente. Prima erano sette scritture in fila senza rete: un guasto dopo
    /// la copia lasciava il gruppo nella bozza ACC con l'ente ancora «suo», e il secondo tentativo ne faceva un
    /// secondo gruppo identico.
    /// </summary>
    [Fact]
    public async Task Un_guasto_a_meta_non_lascia_niente_e_il_secondo_tentativo_fa_un_gruppo_solo()
    {
        var guasto = Servizio(new ProfiloCheSiRompe());
        await Assert.ThrowsAsync<InvalidOperationException>(() => guasto.RemotizzaAsync(_unitId));

        Assert.Null(await _servizio.SpostamentoInCorsoAsync(_unitId));
        Assert.False(await _db.ContentBlocks.AsNoTracking()
            .AnyAsync(b => b.Body == "Vettoramento verso PISATOKEN" && b.Section!.DocumentVersion!.DocumentId != _appDoc));

        await _servizio.RemotizzaAsync(_unitId);
        var modello = await _acc.LoadForEditAsync("LIRR");
        Assert.Single(modello.Blocks, b => b.Block.Kind == AccBlockKind.AppGroup);
    }

    /// <summary>Revisione (S52): una sezione il cui padre sta in un'altra versione non finisce più sotto il blocco in
    /// silenzio — «crea bozza» sullo stesso albero si ferma apposta, e la copia fa lo stesso, senza lasciare niente.</summary>
    [Fact]
    public async Task Una_sezione_orfana_ferma_lo_spostamento_senza_lasciare_niente()
    {
        var accDoc = await _acc.EnsureAsync("LIRR");
        var altrove = await _db.DocumentSections.Where(s => s.DocumentVersion!.DocumentId == accDoc).Select(s => s.Id).FirstAsync();
        var bozza = await _db.DocumentVersions.Where(v => v.DocumentId == _appDoc).Select(v => v.Id).SingleAsync();
        _db.DocumentSections.Add(new DocumentSection
        {
            DocumentVersionId = bozza, ParentSectionId = altrove, Title = "Orfana", Order = 99, Depth = 1,
            SectionKey = SectionKeys.NewCustom(), RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _servizio.RemotizzaAsync(_unitId));
        Assert.Contains("Orfana", ex.Message);
        Assert.Null(await _servizio.SpostamentoInCorsoAsync(_unitId));
    }

    /// <summary>Revisione (S52): un ente senza posizioni farebbe un gruppo senza membri, che nessuna vista live trova.</summary>
    [Fact]
    public async Task Un_ente_senza_posizioni_non_si_sposta()
    {
        await _enti.RemovePositionAsync(_unitId, "LIRP_APP");

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _servizio.RemotizzaAsync(_unitId));
        Assert.Null(await _servizio.SpostamentoInCorsoAsync(_unitId));
    }

    private sealed class ProfiloCheSiRompe : Vipi.Application.Abstractions.IDocumentProfileRepository
    {
        public Task<Vipi.Application.Abstractions.DocumentProfileData> GetAsync(int documentId, CancellationToken ct = default) =>
            throw new InvalidOperationException("guasto finto dopo la copia");
        public Task SaveFreqOrderAsync(int documentId, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveFreqLinksAsync(int documentId, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveHiddenFrequenciesAsync(int documentId, IReadOnlyList<string> callsigns, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>Anche mentre lo spostamento è in corso (S52): un secondo gesto, o una seconda scheda, farebbe un
    /// secondo gruppo identico.</summary>
    [Fact]
    public async Task Un_ente_gia_remotizzato_non_si_sposta_due_volte()
    {
        await _servizio.RemotizzaAsync(_unitId);

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _servizio.RemotizzaAsync(_unitId));
        var modello = await _acc.LoadForEditAsync("LIRR");
        Assert.Single(modello.Blocks, b => b.Block.Kind == AccBlockKind.AppGroup);
    }

    [Fact]
    public async Task Con_la_vipi_acc_in_modifica_da_un_altro_non_si_tocca_niente()
    {
        var accDoc = await _acc.EnsureAsync("LIRR");
        await _editing.AcquireOrInspectLockAsync(accDoc, 1234, "Un altro", 30);

        await Assert.ThrowsAsync<EditConflictException>(() => _servizio.RemotizzaAsync(_unitId));

        Assert.Equal(AtcUnitMode.OwnDocument, (await _enti.FindAsync("LIRP_APP"))!.Mode);
        Assert.False((await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == _appDoc)).IsHidden);
    }

    private sealed class Authz : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Editor;
        public bool IsAdmin => false;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
