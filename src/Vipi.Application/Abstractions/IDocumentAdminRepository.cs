using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Persistenza dell'elenco unificato dei documenti gestibili + hide/delete (Document + profili ACC/APP).</summary>
public interface IDocumentAdminRepository
{
    /// <summary>Elenco unificato (una query sul modello unificato Document). Senza versioni/release.</summary>
    Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default);

    /// <summary>Codice ACC del documento (per l'autorizzazione ACC-scoped). null se non risolvibile.</summary>
    Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default);

    /// <summary>Imposta/azzera il flag nascosto (reversibile). <paramref name="actorUserId"/> finisce nel registro
    /// di audit: cambiare la visibilità pubblica di un documento è un atto amministrativo, non una preferenza.</summary>
    Task SetHiddenAsync(ManagedDocRef doc, bool hidden, int actorUserId, CancellationToken ct = default);

    /// <summary>Cancella definitivamente il documento (+ release orfane, + cascade EF per i Document).
    /// <paramref name="actorUserId"/> finisce nel registro di audit: è l'atto meno reversibile dell'applicazione.</summary>
    Task DeleteAsync(ManagedDocRef doc, int actorUserId, CancellationToken ct = default);
}
