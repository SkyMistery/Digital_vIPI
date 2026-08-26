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

    /// <summary>
    /// Vero se il UserId ha <b>almeno una</b> concessione, su qualunque ACC. Serve alla barra, che deve solo
    /// decidere se accendere il tasto «Modifica»: una domanda sola invece di una per documento.
    /// </summary>
    Task<bool> HasAnyGrantAsync(int UserId, CancellationToken ct = default);

    /// <summary>Codice ACC a cui appartiene un documento (vIPI via settori di scope, vLOA via parte Home). Null se non risolvibile.</summary>
    Task<string?> GetDocumentAccCodeAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// I codici ACC su cui il UserId ha una concessione. Vuoto = nessuna.
    ///
    /// <para>⚠️ Esiste per gli <b>elenchi</b>, dove <see cref="HasGrantAsync"/> e
    /// <c>CanEditDocumentAsync</c> costerebbero due query <b>per riga</b>: su una lista di lavoro con
    /// quaranta voci sono ottanta interrogazioni per rispondere quattro volte la stessa cosa. Si chiedono le
    /// ACC una volta e si filtra in memoria — regola 136, «una query per pagina, non una per riga».</para>
    /// </summary>
    Task<IReadOnlyList<string>> ListAccCodesForUserAsync(int userId, CancellationToken ct = default);
}
