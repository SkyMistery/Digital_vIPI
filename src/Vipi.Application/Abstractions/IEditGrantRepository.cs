using Vipi.Application.Auth;

namespace Vipi.Application.Abstractions;

/// <summary>Porta di accesso alle concessioni di editing (per-FIR) e alla risoluzione FIR di un documento. Impl. EF.</summary>
public interface IEditGrantRepository
{
    Task<IReadOnlyList<GrantRow>> ListAsync(CancellationToken ct = default);
    Task<int> AddAsync(int UserId, string? displayName, string firCode, int GrantedByUserId, CancellationToken ct = default);
    Task RevokeAsync(int grantId, CancellationToken ct = default);

    /// <summary>Vero se il UserId ha una concessione attiva per la FIR indicata.</summary>
    Task<bool> HasGrantAsync(int UserId, string firCode, CancellationToken ct = default);

    /// <summary>Codice FIR a cui appartiene un documento (vIPI via settori di scope, vLOA via parte Home). Null se non risolvibile.</summary>
    Task<string?> GetDocumentFirCodeAsync(int documentId, CancellationToken ct = default);
}
