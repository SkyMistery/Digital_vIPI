using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>
/// Gestione admin dei documenti nell'elenco unificato (Bozze &amp; versioni): elenco, nascondi (reversibile),
/// elimina (definitivo). Scritture gated ACC (admin o grant sull'ACC del documento).
/// </summary>
public interface IDocumentAdminService
{
    Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default);
    Task SetHiddenAsync(ManagedDocRef doc, bool hidden, CancellationToken ct = default);
    Task DeleteAsync(ManagedDocRef doc, CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentAdminService"/>
public sealed class DocumentAdminService : IDocumentAdminService
{
    private readonly IDocumentAdminRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public DocumentAdminService(IDocumentAdminRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default) => _repo.ListAsync(ct);

    public async Task SetHiddenAsync(ManagedDocRef doc, bool hidden, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(doc, ct);
        await _repo.SetHiddenAsync(doc, hidden, ct);
    }

    public async Task DeleteAsync(ManagedDocRef doc, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(doc, ct);
        await _repo.DeleteAsync(doc, ct);
    }

    private async Task EnsureCanEditAsync(ManagedDocRef doc, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeAsync(doc, ct)
            ?? throw new Aor.ValidationException("Documento inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }
}
