using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>
/// Gestione (admin) della policy di import globale: il gestore del sito decide quali categorie di dati
/// arrivano dalla sorgente (sola lettura) e quali restano manuali. Lettura libera, scrittura solo admin.
/// </summary>
public interface IImportPolicyService
{
    Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default);
    Task SaveAsync(ImportPolicySnapshot policy, CancellationToken ct = default);
}

/// <inheritdoc cref="IImportPolicyService"/>
public sealed class ImportPolicyService : IImportPolicyService
{
    private readonly IImportPolicyStore _store;
    private readonly IEditAuthorizationService _authz;

    public ImportPolicyService(IImportPolicyStore store, IEditAuthorizationService authz)
    {
        _store = store;
        _authz = authz;
    }

    public Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default) => _store.GetAsync(ct);

    public Task SaveAsync(ImportPolicySnapshot policy, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _store.SaveAsync(policy, _authz.CurrentUserId ?? 0, ct);
    }
}
