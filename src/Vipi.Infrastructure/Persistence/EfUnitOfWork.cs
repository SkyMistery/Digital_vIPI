using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IUnitOfWork"/>: transazione sul <see cref="VipiDbContext"/> scoped.
/// Usa l'execution strategy del provider (retry-safe dove supportato); tutte le operazioni interne condividono
/// lo stesso context scoped, quindi la stessa transazione.</summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly VipiDbContext _db;
    public EfUnitOfWork(VipiDbContext db) => _db = db;

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await action(ct);
            await tx.CommitAsync(ct);
        });
    }
}
