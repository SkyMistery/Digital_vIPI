using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'interceptor che dice al giro della deriva che sono cambiati dati che finiscono nei documenti. Tre cose che
/// solo un salvataggio vero può dire: che segnala le famiglie giuste, che le scritture del giro stesso non contano
/// (sarebbe un anello), e che una transazione annullata non segnala niente. Carta
/// <c>docs/feature/2026-09-23-da-fare-per-cambiamento.md</c> §4.
/// </summary>
public class SegnalaModificheInterceptorTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly Registro _registro = new();
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite(_conn)
            .AddInterceptors(new SegnalaModificheInterceptor(_registro))
            .Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _registro.Segnalate.Clear();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private Document Doc() => new()
    {
        Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, Status = DocumentStatus.Draft,
        LastUpdatedAiracCycle = "2610",
    };

    [Fact]
    public async Task Scrivere_un_documento_segnala_la_sua_famiglia()
    {
        _db.Documents.Add(Doc());
        await _db.SaveChangesAsync();

        Assert.Equal(new[] { FamiglieDiModifica.Testo }, Assert.Single(_registro.Segnalate));
    }

    [Fact]
    public async Task Le_scritture_del_giro_non_segnalano()
    {
        var d = Doc();
        _db.Documents.Add(d);
        await _db.SaveChangesAsync();
        _registro.Segnalate.Clear();

        _db.DocumentImpacts.Add(new DocumentImpact
        {
            DocumentId = d.Id, Kind = ImpactKind.ReleaseDrift, SourceKey = "k", ReasonKey = "x", RaisedUtc = DateTime.UtcNow,
        });
        _db.EditorTasks.Add(new EditorTask { Title = "t", CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        Assert.Empty(_registro.Segnalate);
    }

    [Fact]
    public async Task Dentro_una_transazione_si_segnala_alla_conferma()
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Documents.Add(Doc());
        await _db.SaveChangesAsync();
        Assert.Empty(_registro.Segnalate);   // non ancora visibile agli altri

        await tx.CommitAsync();
        Assert.Single(_registro.Segnalate);
    }

    [Fact]
    public async Task Una_transazione_annullata_non_segnala_niente()
    {
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            _db.Documents.Add(Doc());
            await _db.SaveChangesAsync();
            await tx.RollbackAsync();
        }

        Assert.Empty(_registro.Segnalate);

        // E il salvataggio dopo non si porta dietro le famiglie di quello annullato.
        _db.ChangeTracker.Clear();
        _db.Accs.Add(new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" });
        await _db.SaveChangesAsync();
        Assert.Equal(new[] { FamiglieDiModifica.Settori }, Assert.Single(_registro.Segnalate));
    }

    private sealed class Registro : IModificheInAttesa
    {
        public List<string[]> Segnalate { get; } = new();

        public void Segnala(IReadOnlyCollection<string> famiglie, DateTime quandoUtc) =>
            Segnalate.Add(famiglie.OrderBy(f => f, StringComparer.Ordinal).ToArray());

        public Task<FinestraDiModifiche> PrendiAsync(TimeSpan attesa, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
