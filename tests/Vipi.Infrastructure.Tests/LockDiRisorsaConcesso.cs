using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Lock di risorsa sempre in mano: per i test che provano <b>che cosa</b> fanno i servizi della struttura, non
/// <b>chi</b> può chiamarli. Il lock vero si prova in <c>LockDellaStrutturaTests</c> (T-025).
/// </summary>
internal sealed class LockDiRisorsaConcesso : IResourceLockService
{
    public static readonly LockDiRisorsaConcesso Instance = new();
    public Task<LockInfo> AcquireAsync(string resourceKey, CancellationToken ct = default) => Task.FromResult(LockInfo.Free());
    public Task<LockInfo> InspectAsync(string resourceKey, CancellationToken ct = default) => Task.FromResult(LockInfo.Free());
    public Task<LockInfo> HeartbeatAsync(string resourceKey, CancellationToken ct = default) => Task.FromResult(LockInfo.Free());
    public Task ReleaseAsync(string resourceKey, CancellationToken ct = default) => Task.CompletedTask;
    public Task ForceUnlockAsync(string resourceKey, CancellationToken ct = default) => Task.CompletedTask;
    public Task EnsureHeldAsync(string resourceKey, CancellationToken ct = default) => Task.CompletedTask;
}
