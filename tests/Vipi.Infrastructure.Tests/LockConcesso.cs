using Vipi.Application.Content;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Guardia di lock che dice sempre sì: per i test che provano <b>che cosa</b> si salva, non <b>chi</b> può
/// salvare. Il lock vero si prova in <c>LockDelleScrittureStrutturateTests</c> (T-004).
/// </summary>
internal sealed class LockConcesso : IDocumentLockGuard
{
    public static readonly LockConcesso Instance = new();
    public Task EnsureMineAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
}
