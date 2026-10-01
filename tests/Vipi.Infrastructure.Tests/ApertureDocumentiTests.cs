using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Il contatore delle aperture su SQLite (S93, 1° ottobre 2026; carta 2026-10-01-aperture-documenti.md).</summary>
public sealed class ApertureDocumentiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private readonly Orologio _ora = new(new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc));
    private int _a, _b;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        var a = new Document { Type = DocumentType.Vipi, Title = "vIPI — LIRP", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        var b = new Document { Type = DocumentType.Vipi, Title = "vSOP MIL — LIRP", Language = Language.En, LastUpdatedAiracCycle = "2610" };
        _db.Documents.AddRange(a, b);
        await _db.SaveChangesAsync();
        (_a, _b) = (a.Id, b.Id);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private sealed class Orologio : TimeProvider
    {
        public DateTime Adesso;
        public Orologio(DateTime adesso) => Adesso = adesso;
        public override DateTimeOffset GetUtcNow() => new(Adesso);
    }

    /// <summary>Ogni chiave è l'ID del documento scritto in cifre; «?» non si risolve; «!» rompe.</summary>
    private sealed class Bersagli : IReleaseTargetRegistry, IReleaseTarget
    {
        public IReleaseTarget For(ReleaseTargetType type) => this;
        public IReadOnlyList<IReleaseTarget> ByDescribeOrder => new IReleaseTarget[] { this };
        public ReleaseTargetType Type => ReleaseTargetType.Airport;
        public int DescribeOrder => 1;
        public Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
            key == "!" ? throw new InvalidOperationException("archivio giù")
                       : Task.FromResult(int.TryParse(key, out var id) ? id : (int?)null);
        public Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>("LIRR");
        public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed) { managed = default!; return false; }
    }

    private EfApertureDocumenti Contatore() =>
        new(_db, new Bersagli(), NullLogger<EfApertureDocumenti>.Instance, _ora);

    [Fact]
    public async Task Stesso_giorno_una_riga_che_cresce_giorno_nuovo_riga_nuova()
    {
        var c = Contatore();
        await c.SegnaAsync(ReleaseTargetType.Airport, _a.ToString());
        await c.SegnaAsync(ReleaseTargetType.Airport, _a.ToString());
        _ora.Adesso = _ora.Adesso.AddDays(1);
        await c.SegnaAsync(ReleaseTargetType.Airport, _a.ToString());

        var righe = await _db.AperturaDocumenti.AsNoTracking().OrderBy(r => r.Giorno).ToListAsync();
        Assert.Equal(new[] { 2, 1 }, righe.Select(r => r.Volte));
        Assert.Equal(new DateTime(2026, 10, 1), righe[0].Giorno.Date);
    }

    /// <summary>🔴 L'incremento lo fa il database: un secondo contatore con un contesto suo, che non ha mai visto la
    /// riga, deve aggiungersi a quella e non crearne un'altra né sovrascriverla.</summary>
    [Fact]
    public async Task Due_contesti_sullo_stesso_documento_si_sommano()
    {
        await Contatore().SegnaAsync(ReleaseTargetType.Airport, _a.ToString());
        await using var altro = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await new EfApertureDocumenti(altro, new Bersagli(), NullLogger<EfApertureDocumenti>.Instance, _ora)
            .SegnaAsync(ReleaseTargetType.Airport, _a.ToString());

        var riga = Assert.Single(await _db.AperturaDocumenti.AsNoTracking().ToListAsync());
        Assert.Equal(2, riga.Volte);
    }

    [Fact]
    public async Task Gli_ultimi_90_giorni_contano_il_91esimo_no()
    {
        var c = Contatore();
        var oggi = _ora.Adesso;
        _ora.Adesso = oggi.AddDays(-89);
        await c.SegnaAsync(ReleaseTargetType.Airport, _a.ToString());   // dentro: è l'ultimo giorno della finestra
        _ora.Adesso = oggi.AddDays(-90);
        await c.SegnaAsync(ReleaseTargetType.Airport, _a.ToString());   // fuori
        await c.SegnaAsync(ReleaseTargetType.Airport, _b.ToString());   // fuori
        _ora.Adesso = oggi;
        await c.SegnaAsync(ReleaseTargetType.Airport, _a.ToString());

        var recenti = await c.RecentiAsync();

        Assert.Equal(2, recenti[_a]);
        Assert.False(recenti.ContainsKey(_b));
    }

    /// <summary>⚠️ Il contatore non porta mai giù la pagina: un documento che non si risolve non conta, un archivio che
    /// non risponde finisce nel registro.</summary>
    [Fact]
    public async Task Chiave_sconosciuta_o_guasto_non_lanciano_e_non_scrivono()
    {
        var c = Contatore();
        await c.SegnaAsync(ReleaseTargetType.Airport, "?");
        await c.SegnaAsync(ReleaseTargetType.Airport, "!");

        Assert.Empty(await _db.AperturaDocumenti.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Un_documento_cancellato_si_porta_via_le_sue_aperture()
    {
        await Contatore().SegnaAsync(ReleaseTargetType.Airport, _a.ToString());
        _db.ChangeTracker.Clear();
        await _db.Documents.Where(d => d.Id == _a).ExecuteDeleteAsync();

        Assert.Empty(await _db.AperturaDocumenti.AsNoTracking().ToListAsync());
    }
}
