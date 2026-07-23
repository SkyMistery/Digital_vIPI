using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Stato degli import periodici: successo azzera l'errore precedente; fallimento registra
/// LastAttemptUtc+LastError senza toccare LastSuccessUtc; GetAll restituisce tutte le righe.
/// </summary>
public class ImportStateStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfImportStateStore _store = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfImportStateStore(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Failure_records_error_and_preserves_last_success()
    {
        var ok = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        await _store.MarkSuccessAsync(ImportCategories.Acc, ok);

        var failAt = ok.AddHours(1);
        await _store.MarkFailureAsync(ImportCategories.Acc, failAt, "sorgente 503");

        var row = Assert.Single(await _store.GetAllAsync());
        Assert.Equal(ok, row.LastSuccessUtc);          // il successo storico resta
        Assert.Equal(failAt, row.LastAttemptUtc);      // ultimo tentativo = il fallimento
        Assert.Equal("sorgente 503", row.LastError);
    }

    [Fact]
    public async Task Success_after_failure_clears_error()
    {
        await _store.MarkFailureAsync(ImportCategories.Sid, DateTime.UtcNow, "boom");
        var recovered = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        await _store.MarkSuccessAsync(ImportCategories.Sid, recovered);

        var row = Assert.Single(await _store.GetAllAsync());
        Assert.Null(row.LastError);
        Assert.Equal(recovered, row.LastSuccessUtc);
        Assert.Equal(recovered, row.LastAttemptUtc);
    }

    [Fact]
    public async Task Failure_on_new_category_leaves_last_success_default()
    {
        await _store.MarkFailureAsync(ImportCategories.SpecialArea, DateTime.UtcNow, "mai riuscito");

        var row = Assert.Single(await _store.GetAllAsync());
        Assert.Equal(default, row.LastSuccessUtc);     // nessun successo mai avvenuto
        Assert.Equal("mai riuscito", row.LastError);
    }
}
