using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using static Vipi.Application.Messaggio;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Gestione admin dei documenti nell'elenco unificato (Bozze &amp; versioni): elenco, nascondi (reversibile),
/// elimina (definitivo). Scritture gated ACC (admin o grant sull'ACC del documento) <b>e</b> gated dal lock di
/// editing: non si tocca un documento che un'altra persona sta modificando.
/// </summary>
public interface IDocumentAdminService
{
    Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default);
    Task SetHiddenAsync(ManagedDocRef doc, bool hidden, CancellationToken ct = default);

    /// <inheritdoc cref="IDocumentAdminRepository.GetLanguageAsync"/>
    Task<DocumentLanguageState?> GetLanguageAsync(ManagedDocRef doc, CancellationToken ct = default);

    /// <inheritdoc cref="IDocumentAdminRepository.SetLanguageAsync"/>
    Task SetLanguageAsync(ManagedDocRef doc, Language language, bool locked, CancellationToken ct = default);
    Task DeleteAsync(ManagedDocRef doc, CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentAdminService"/>
public sealed class DocumentAdminService : IDocumentAdminService
{
    private readonly IDocumentAdminRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IEditingRepository _editing;

    /// <summary>Le righe delle unioni, per rimettere in ordine dopo un'eliminazione. Opzionale: senza,
    /// l'unione rimasta con un membro solo la chiude comunque il giro d'avvio.</summary>
    private readonly Abstractions.IDocumentUnionRepository? _unioni;

    public DocumentAdminService(IDocumentAdminRepository repo, IEditAuthorizationService authz,
                                IEditingRepository editing,
                                Abstractions.IDocumentUnionRepository? unioni = null)
    {
        _repo = repo;
        _authz = authz;
        _editing = editing;
        _unioni = unioni;
    }

    public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default) => _repo.ListAsync(ct);

    public Task<DocumentLanguageState?> GetLanguageAsync(ManagedDocRef doc, CancellationToken ct = default) =>
        _repo.GetLanguageAsync(doc, ct);

    public async Task SetLanguageAsync(ManagedDocRef doc, Language language, bool locked, CancellationToken ct = default)
    {
        // Gli stessi due cancelli di «nascondi», e per la stessa ragione: bloccare la lingua di un documento
        // pubblicato cambia quel che il pubblico legge, e non si fa mentre un'altra persona lo sta scrivendo.
        await EnsureCanEditAsync(doc, ct);
        await EnsureNotLockedByOtherAsync(doc, ct);
        await _repo.SetLanguageAsync(doc, language, locked, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task SetHiddenAsync(ManagedDocRef doc, bool hidden, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(doc, ct);
        await EnsureNotLockedByOtherAsync(doc, ct);
        await _repo.SetHiddenAsync(doc, hidden, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task DeleteAsync(ManagedDocRef doc, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(doc, ct);
        await EnsureNotLockedByOtherAsync(doc, ct);
        await _repo.DeleteAsync(doc, _authz.CurrentUserId ?? 0, ct);

        // Il documento se n'e' andato, e con lui la sua riga di appartenenza (FK cascade). Quel che resta da
        // chiudere e' l'UNIONE che quella riga teneva in piedi.
        // ⚠️ Un'unione con un membro solo e' una pagina unita che unisce se' stessa, e un redirect che non
        // ha dove mandare: chi apre l'altro membro finirebbe su un indirizzo che non esiste piu'. Il giro
        // d'avvio la chiuderebbe comunque, ma al prossimo riavvio — cioe' non adesso, che e' quando serve.
        if (_unioni is not null) await _unioni.TidyAsync(ct);
    }

    /// <summary>
    /// Rifiuta la scrittura se il documento ha un lock di editing di <b>un'altra</b> persona.
    ///
    /// <para><b>Perché qui e non nel bottone.</b> Fino al 21 agosto 2026 queste due chiamate guardavano solo il
    /// grant ACC: si poteva eliminare un documento mentre qualcuno lo stava editando, e quella persona lo
    /// scopriva al salvataggio, con il lavoro già perso. Spegnere il tasto nella pagina non basta — l'elenco è
    /// una fotografia, e chi arriva da un'altra scheda o con la lista vecchia in mano passerebbe lo stesso.</para>
    ///
    /// <para>Il lock <b>scaduto</b> non blocca: <c>InspectLockAsync</c> lo riporta come libero. Un lock altrui
    /// vivo si toglie dall'elenco col force-unlock (admin), non aggirandolo qui.</para>
    /// </summary>
    private async Task EnsureNotLockedByOtherAsync(ManagedDocRef doc, CancellationToken ct)
    {
        if (doc.DocumentId is not int id) return;
        var lk = await _editing.InspectLockAsync(id, _authz.CurrentUserId ?? 0, ct);
        if (!lk.Locked || lk.IsMine) return;
        throw new EditConflictException(Lingua(
            $"Documento in modifica da {lk.ByName ?? $"VID {lk.ByUserId}"} fino alle {lk.ExpiresUtc:HH:mm} UTC: "
            + "aspetta che finisca, oppure sbloccalo (solo admin) prima di procedere.",
            $"Document being edited by {lk.ByName ?? $"VID {lk.ByUserId}"} until {lk.ExpiresUtc:HH:mm} UTC: "
            + "wait until they are done, or unlock it (admin only) before carrying on."));
    }

    private async Task EnsureCanEditAsync(ManagedDocRef doc, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeAsync(doc, ct)
            ?? throw new Aor.ValidationException(Lingua("Documento inesistente.", "The document does not exist."));
        _authz.EnsureAtLeast(VipiRole.Editor);
    }
}
