namespace Vipi.Application.Abstractions;

/// <summary>Una riga di appartenenza: quale documento sta in quale unione, e in che posizione.</summary>
/// <param name="UnionId">L'unione.</param>
/// <param name="MemberId">La riga di appartenenza — è ciò che si sposta e si toglie.</param>
/// <param name="DocumentId">Il documento.</param>
/// <param name="Order">La posizione, 0-based e densa. La minore è l'OSPITE.</param>
public sealed record UnionRow(int UnionId, int MemberId, int DocumentId, int Order);

/// <summary>
/// La persistenza delle unioni di documenti (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c>).
///
/// <para>Solo righe: chi può unire, che cosa si può unire e quando un'unione smette di essere tale sono
/// decisioni di dominio e stanno in <c>IDocumentUnionService</c>. Qui dentro non si autorizza niente.</para>
/// </summary>
public interface IDocumentUnionRepository
{
    /// <summary>Tutte le appartenenze, per la proiezione dell'elenco unificato. Sono poche righe: la
    /// divisione ha una manciata di unioni, non una per documento.</summary>
    Task<IReadOnlyList<UnionRow>> ListAsync(CancellationToken ct = default);

    /// <summary>Le righe dell'unione a cui appartiene questo documento, in ordine. Vuoto = non è unito.</summary>
    Task<IReadOnlyList<UnionRow>> ByDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>Le righe di questa unione, in ordine.</summary>
    Task<IReadOnlyList<UnionRow>> ByUnionAsync(int unionId, CancellationToken ct = default);

    /// <summary>
    /// Crea un'unione con questi due documenti, nell'ordine dato, e ne ritorna l'id.
    /// <para>⚠️ Un'unione nasce sempre con <b>due</b> membri: una con uno solo non è un'unione, e lasciarla
    /// creare vuota vorrebbe dire ammettere uno stato che nessuna pagina sa mostrare.</para>
    /// </summary>
    Task<int> CreateAsync(int hostDocumentId, int guestDocumentId, int createdByUserId,
                          CancellationToken ct = default);

    /// <summary>Aggiunge un documento in coda a un'unione che c'è già.</summary>
    Task AddMemberAsync(int unionId, int documentId, CancellationToken ct = default);

    /// <summary>Toglie una riga di appartenenza e <b>ricompatta</b> le posizioni rimaste.</summary>
    Task RemoveMemberAsync(int memberId, CancellationToken ct = default);

    /// <summary>Scioglie l'unione: via i membri, via l'unione. I documenti non si toccano.</summary>
    Task DissolveAsync(int unionId, CancellationToken ct = default);

    /// <summary>
    /// Sposta un membro di una posizione, scambiandolo con il vicino: <paramref name="delta"/> −1 su, +1 giù.
    /// Ai bordi non fa niente. È lo stesso gesto di <c>EfEditingRepository.MoveSectionAsync</c>, un piano più su.
    /// </summary>
    Task MoveAsync(int memberId, int delta, CancellationToken ct = default);

    /// <summary>
    /// Toglie dalle unioni i documenti che non esistono più e scioglie quel che resta di un'unione con meno
    /// di due membri. Idempotente: gira all'avvio e dopo ogni eliminazione di documento.
    /// <para>⚠️ La FK cancella già la riga di appartenenza insieme al documento — questo giro serve al
    /// <b>dopo</b>: un'unione rimasta con un membro solo è una pagina unita che unisce sé stessa.</para>
    /// </summary>
    Task<int> TidyAsync(CancellationToken ct = default);
}
