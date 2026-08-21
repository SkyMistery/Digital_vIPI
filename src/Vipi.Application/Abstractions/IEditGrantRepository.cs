using Vipi.Application.Auth;

namespace Vipi.Application.Abstractions;

/// <summary>Porta di accesso alle concessioni di editing (per-ACC) e alla risoluzione ACC di un documento. Impl. EF.</summary>
public interface IEditGrantRepository
{
    Task<IReadOnlyList<GrantRow>> ListAsync(CancellationToken ct = default);
    Task<int> AddAsync(int UserId, string? displayName, string accCode, int GrantedByUserId, CancellationToken ct = default);
    /// <summary>Revoca la concessione. <paramref name="actorUserId"/> è <b>chi revoca</b>: fino al 22 agosto 2026
    /// il registro di audit ci scriveva chi aveva <i>concesso</i>, cioè attribuiva l'atto alla persona sbagliata.</summary>
    Task RevokeAsync(int grantId, int actorUserId, CancellationToken ct = default);

    /// <summary>Vero se il UserId ha una concessione attiva per la ACC indicata.</summary>
    Task<bool> HasGrantAsync(int UserId, string accCode, CancellationToken ct = default);

    /// <summary>Codice ACC a cui appartiene un documento (vIPI via settori di scope, vLOA via parte Home). Null se non risolvibile.</summary>
    Task<string?> GetDocumentAccCodeAsync(int documentId, CancellationToken ct = default);
}
