using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IUnitOfWork"/>: transazione sul <see cref="VipiDbContext"/> scoped.
/// Usa l'execution strategy del provider (retry sui guasti transitori dove supportato); tutte le operazioni interne
/// condividono lo stesso context scoped, quindi la stessa transazione.</summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly VipiDbContext _db;
    public EfUnitOfWork(VipiDbContext db) => _db = db;

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async token =>
        {
            // Ogni tentativo riparte da un change-tracker pulito. Avvolgere il blocco nell'execution strategy è
            // NECESSARIO ma non basta: al retry (Npgsql EnableRetryOnFailure su Neon) la strategy rigira questa
            // lambda sullo STESSO context scoped, e il rollback della transazione NON ripulisce il tracker: le
            // entità Added/Modified del tentativo fallito sopravvivono e vengono reinserite insieme a quelle del
            // nuovo tentativo (doppi insert / modifiche sommate). Le operazioni dei chiamanti sono upsert che
            // rileggono da zero, quindi azzerare il tracker rende ogni tentativo idempotente.
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(token);
            await action(token);
            await tx.CommitAsync(token);
        }, ct);
    }
}
