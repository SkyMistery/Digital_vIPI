using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>
/// Il lock del documento come guardia delle scritture <b>strutturate</b> (T-004, revisione del 13 settembre
/// 2026): separazioni, VFR, configurazioni, aree, AoR, frequenze di APP e ACC, e le tabelle del vSOP militare.
///
/// <para><b>Perché serve.</b> Quelle scritture riscrivono per intero il <c>BodyJson</c> di una sezione, e fino
/// al 13 settembre controllavano soltanto il ruolo. Chi aveva perso il lock — scaduto, o tolto da «sblocca
/// comunque» — continuava a salvare dalla pagina aperta, sopra il lavoro di chi il lock l'aveva preso dopo.
/// Un salvataggio di prosa dello stesso editor veniva invece rifiutato: la regola c'era, in un posto solo
/// (<see cref="EditingService"/>). È la stessa di <see cref="IAirportLockGuard"/> per gli scali: un tasto spento
/// non è una guardia.</para>
///
/// <para>Come la prosa, chi scrive col lock lo <b>rinnova</b>: lavorare è restare in modifica.</para>
/// </summary>
public interface IDocumentLockGuard
{
    /// <summary>Pretende che il lock del documento sia dell'utente corrente, e lo rinnova. Altrimenti
    /// <see cref="EditConflictException"/>, che gli editor sanno già mostrare.</summary>
    Task EnsureMineAsync(int documentId, CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentLockGuard"/>
public sealed class DocumentLockGuard : IDocumentLockGuard
{
    /// <summary>Lo stesso TTL del lock di documento preso dall'editor (<see cref="EditingService"/>).</summary>
    public const int LockTtlMinutes = 30;

    private readonly IEditingRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public DocumentLockGuard(IEditingRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    public async Task EnsureMineAsync(int documentId, CancellationToken ct = default)
    {
        var uid = _authz.CurrentUserId ?? 0;
        if (!await _repo.IsLockHeldByAsync(documentId, uid, ct).ConfigureAwait(false))
            throw new EditConflictException(Lingua(
                "Documento bloccato da un altro editor o lock scaduto: riapri l'editor per riacquisirlo.",
                "The document is locked by another editor, or the lock has expired: reopen the editor to take it again."));
        await _repo.RenewLockAsync(documentId, uid, LockTtlMinutes, ct).ConfigureAwait(false);
    }
}
