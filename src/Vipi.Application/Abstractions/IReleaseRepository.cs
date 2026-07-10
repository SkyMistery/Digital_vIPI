using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Riga di release per la timeline/UI.</summary>
public sealed record ReleaseInfo(
    int Id, int VersionNumber, string ReleaseAiracCycle, DateTime ReleaseEffectiveUtc,
    ReleaseStatus Status, int CreatedByUserId, DateTime CreatedUtc, string? Note, bool IsEffectiveNow);

/// <summary>
/// Persistenza delle release AIRAC (snapshot editoriale per ciclo). Lo snapshot dello stato live è generato qui
/// (dispatch per <see cref="ReleaseTargetType"/>); la selezione della release effettiva serve al viewer.
/// </summary>
public interface IReleaseRepository
{
    /// <summary>Serializza lo stato editoriale LIVE del bersaglio in un payload JSON (snapshot), stampando
    /// <paramref name="airacCycle"/> come ciclo del documento. null se il bersaglio non esiste.</summary>
    Task<string?> SnapshotWorkingAsync(ReleaseTargetType type, string key, string airacCycle, CancellationToken ct = default);

    /// <summary>Inserisce una release col payload dato al ciclo indicato. Assegna VersionNumber, supera (Superseded)
    /// eventuali release non-superate dello stesso ciclo, ricalcola gli stati (Scheduled/Effective). Ritorna l'Id.</summary>
    Task<int> SaveReleaseAsync(ReleaseTargetType type, string key, string releaseCycle, DateTime effectiveUtc,
        string payloadJson, int createdByUserId, string? note, CancellationToken ct = default);

    /// <summary>Promuove a Published la bozza di lavorazione del Document bersaglio (stessa semantica del publish-versione
    /// dell'editor): archivia la pubblicata precedente, versione→Published, imposta CurrentVersionId e Document.Status.
    /// Usato dalla pubblicazione immediata (review) perché il documento sia visibile anche nelle liste (gate su
    /// Status==Published), non solo via snapshot di release. No-op se non c'è una bozza da promuovere. Vedi
    /// <c>EfEditingRepository.PublishAsync</c> (publish-versione canonico dall'editor).</summary>
    Task PublishWorkingVersionAsync(ReleaseTargetType type, string key, int actorUserId, string airacCycle, CancellationToken ct = default);

    /// <summary>Release del bersaglio, più recenti prima (per la timeline).</summary>
    Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>La release effettiva a <paramref name="atUtc"/> (ReleaseEffectiveUtc &lt;= atUtc, più recente), o null.</summary>
    Task<DocRelease?> GetEffectiveAsync(ReleaseTargetType type, string key, DateTime atUtc, CancellationToken ct = default);

    /// <summary>Una release per Id (col payload), per anteprima/diff. null se inesistente.</summary>
    Task<DocRelease?> GetByIdAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Annulla (elimina) una release; ricalcola gli stati delle rimanenti dello stesso bersaglio. Ritorna
    /// (TargetType, TargetKey) della release rimossa per l'autorizzazione, o null se inesistente.</summary>
    Task<(ReleaseTargetType Type, string Key)?> CancelAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Codice ACC da autorizzare per pubblicare release del bersaglio (Home per vLOA, ACC dell'aeroporto/settore
    /// per gli altri, il codice stesso per ACC). null se non risolvibile.</summary>
    Task<string?> GetAuthAccCodeAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>Riepilogo release (in vigore / prossima schedulata) per un insieme di bersagli, in un'unica query.
    /// Chiave del dizionario = (TargetType, TargetKey). I bersagli senza release non compaiono.</summary>
    Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
        IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default);
}
