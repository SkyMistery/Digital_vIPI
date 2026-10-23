using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il testo comune delle torri radar in archivio (S127, carta <c>2026-10-22-testo-comune.md</c>): una riga di
/// <c>SharedBlocks</c> come le intro, scritta solo dagli admin col lock, e letta da tutti.
/// </summary>
public class TestoComuneStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private EfTestoComuneStore Deposito(VipiRole livello = VipiRole.Admin) =>
        new(_db, new Authz(livello), LockDiRisorsaConcesso.Instance);

    private static readonly List<ExtraBlock> DueCallout = new()
    {
        new() { Format = BlockFormat.Callout, CalloutKind = CalloutKind.Danger, Text = "È fatto obbligo la visione del documento." },
        new() { Format = BlockFormat.Callout, CalloutKind = CalloutKind.Success, Text = "LA TORRE NON FORNISCE VETTORAMENTO" },
    };

    private const string Tecnica = SectionKeys.RadarTowerTechnique;

    [Fact]
    public async Task Si_salva_e_si_rilegge_in_un_blocco_condiviso()
    {
        await Deposito().SalvaAsync(Tecnica, DueCallout);

        var letti = await Deposito(VipiRole.User).LeggiAsync(Tecnica);
        Assert.Equal(2, letti.Count);
        Assert.Equal(CalloutKind.Success, letti[1].CalloutKind);

        var riga = Assert.Single(await _db.SharedBlocks.AsNoTracking().ToListAsync());
        Assert.Equal("section-common:radartower:technique", riga.Key);
    }

    /// <summary>Cambia il testo di tutte le torri insieme: lo scrivono solo gli admin, e il cancello è nel deposito.</summary>
    [Theory]
    [InlineData(VipiRole.Editor)]
    [InlineData(VipiRole.DivisionStaff)]
    public async Task Sotto_admin_non_si_salva(VipiRole livello)
    {
        await Assert.ThrowsAsync<EditNotAllowedException>(() => Deposito(livello).SalvaAsync(Tecnica, DueCallout));

        Assert.Empty(await _db.SharedBlocks.AsNoTracking().ToListAsync());
    }

    /// <summary>La riga si riscrive per intero: chi ha perso il lock coprirebbe il lavoro di chi ce l'ha adesso.</summary>
    [Fact]
    public async Task Col_lock_di_un_altro_non_si_salva()
    {
        var authz = new Authz(VipiRole.Admin);
        _db.EditResourceLocks.Add(new Vipi.Domain.Entities.EditResourceLock
        {
            ResourceKey = TestoComune.ChiaveLock(Tecnica), LockedByUserId = 2, LockedByName = "collega",
            LockedAtUtc = DateTime.UtcNow, LockExpiresUtc = DateTime.UtcNow.AddMinutes(3),
        });
        await _db.SaveChangesAsync();
        var deposito = new EfTestoComuneStore(_db, authz, new ResourceLockService(new EfResourceLockRepository(_db), authz));

        await Assert.ThrowsAsync<EditConflictException>(() => deposito.SalvaAsync(Tecnica, DueCallout));

        Assert.Empty(await _db.SharedBlocks.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Svuotato_la_riga_se_ne_va()
    {
        await Deposito().SalvaAsync(Tecnica, DueCallout);
        await Deposito().SalvaAsync(Tecnica, new List<ExtraBlock> { new() { Format = BlockFormat.Prose, Text = " " } });

        Assert.Empty(await _db.SharedBlocks.AsNoTracking().ToListAsync());
        Assert.Empty(await Deposito().LeggiAsync(Tecnica));
    }

    /// <summary>
    /// Il testo comune nasce in italiano e sta fuori dai documenti: senza il pezzo nel corpus la pagina inglese
    /// avrebbe due callout italiani in testa alla tecnica operativa, e nulla protesterebbe.
    /// </summary>
    [Fact]
    public async Task Le_frasi_entrano_nel_giro_della_traduzione_italiana_e_non_in_quella_inglese()
    {
        await Deposito().SalvaAsync(Tecnica, DueCallout);

        Assert.Contains("LA TORRE NON FORNISCE VETTORAMENTO", await new EfTranslatableCorpus(_db).SegmentiAsync("it"));
        Assert.DoesNotContain("LA TORRE NON FORNISCE VETTORAMENTO", await new EfTranslatableCorpus(_db).SegmentiAsync("en"));
    }

    private sealed class Authz : IEditAuthorizationService
    {
        private readonly VipiRole _livello;
        public Authz(VipiRole livello) => _livello = livello;
        public VipiRole Role => _livello;
        public bool IsAdmin => _livello >= VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
    }
}
