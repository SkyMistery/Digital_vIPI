using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Caratterizzazione di <see cref="EfUnitOfWork"/> sul comportamento al retry. L'execution strategy dei provider
/// con <c>EnableRetryOnFailure</c> (Postgres/Neon) rigira la lambda transazionale sullo STESSO context scoped: il
/// rollback della transazione non ripulisce il change-tracker, quindi senza un azzeramento esplicito le entità del
/// tentativo fallito verrebbero riscritte insieme a quelle del tentativo buono.
/// </summary>
public class UnitOfWorkTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static AuditLog NewLog() => new()
    {
        UserId = 1,
        Action = AuditAction.Update,
        EntityType = "Test",
        EntityId = "1",
        TimestampUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task Retry_Non_Riscrive_Le_Entita_Del_Tentativo_Fallito()
    {
        var uow = new EfUnitOfWork(_db);

        // Tentativo 1: traccia una riga, poi fallisce (guasto transitorio simulato) → rollback.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uow.ExecuteInTransactionAsync(async _ =>
            {
                _db.AuditLogs.Add(NewLog());
                await Task.Yield();
                throw new InvalidOperationException("guasto transitorio simulato");
            }));

        // Tentativo 2: è ciò che farebbe l'execution strategy — stesso context, lavoro rifatto da zero.
        await uow.ExecuteInTransactionAsync(async token =>
        {
            _db.AuditLogs.Add(NewLog());
            await _db.SaveChangesAsync(token);
        });

        // Una sola riga: la Added rimasta appesa al primo tentativo non deve essere riemessa.
        Assert.Equal(1, await _db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Il_Tentativo_Vede_Un_Tracker_Pulito()
    {
        var uow = new EfUnitOfWork(_db);
        _db.AuditLogs.Add(NewLog());   // sporcizia pre-esistente nello scope

        var trackedAllIngresso = -1;
        await uow.ExecuteInTransactionAsync(_ =>
        {
            trackedAllIngresso = _db.ChangeTracker.Entries().Count();
            return Task.CompletedTask;
        });

        Assert.Equal(0, trackedAllIngresso);
    }

    [Fact]
    public async Task Commit_Rende_Persistenti_Le_Modifiche()
    {
        var uow = new EfUnitOfWork(_db);

        await uow.ExecuteInTransactionAsync(async token =>
        {
            _db.AuditLogs.Add(NewLog());
            await _db.SaveChangesAsync(token);
        });

        Assert.Equal(1, await _db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Eccezione_Nel_Blocco_Fa_Rollback()
    {
        var uow = new EfUnitOfWork(_db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uow.ExecuteInTransactionAsync(async token =>
            {
                _db.AuditLogs.Add(NewLog());
                await _db.SaveChangesAsync(token);
                throw new InvalidOperationException("boom");
            }));

        _db.ChangeTracker.Clear();
        Assert.Equal(0, await _db.AuditLogs.CountAsync());
    }
}
