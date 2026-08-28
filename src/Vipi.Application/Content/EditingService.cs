using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IEditingService"/>
public sealed class EditingService : IEditingService
{
    private const int LockTtlMinutes = 30;

    private readonly IEditingRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly ReleaseRetentionOptions _retention;

    public EditingService(IEditingRepository repo, IEditAuthorizationService authz, IOptions<ReleaseRetentionOptions> retention)
    {
        _repo = repo;
        _authz = authz;
        _retention = retention.Value;
    }

    // Lista dei documenti = metadati per il picker dell'editor (non sensibile). Le aperture/modifiche sono ACC-gated.
    public Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default) =>
        _repo.ListDocumentsAsync(ct);

    // Picker editor: filtra i documenti per i permessi del UserId corrente (admin = tutti; altri = grant sulla ACC).
    public Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default) =>
        _repo.ListVersionsAsync(documentId, ct);

    public async Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        return await _repo.LoadForEditAsync(documentId, ct);
    }

    public Task<int?> ResolveVloaIdByPairAsync(string homeAccCode, string foreignAccCode, CancellationToken ct = default) =>
        _repo.FindVloaIdByPairAsync((homeAccCode ?? "").Trim().ToUpperInvariant(), (foreignAccCode ?? "").Trim().ToUpperInvariant(), ct);

    public async Task<int> CreateDocumentAsync(DocumentType type, string title, IReadOnlyList<int>? scopeSectorIds,
        int? primarySectorId, int? homeSectorId, int? neighbourSectorId, CancellationToken ct = default)
    {
        title = (title ?? "").Trim();
        if (title.Length == 0) throw new Aor.ValidationException(Lingua("Titolo documento obbligatorio.", "The document title is required."));

        (int, int)? parties = null;
        IReadOnlyList<int>? scope = null;
        int? primary = null;
        string accCode;
        if (type == DocumentType.Vloa)
        {
            if (homeSectorId is not int home || neighbourSectorId is not int neigh)
                throw new Aor.ValidationException(Lingua("La vLOA richiede un settore Home e uno Neighbour.", "A vLOA needs a Home sector and a Neighbour one."));
            if (home == neigh) throw new Aor.ValidationException(Lingua("Home e Neighbour non possono coincidere.", "Home and Neighbour cannot be the same."));
            parties = (home, neigh);
            accCode = await _repo.GetAccCodeBySectorAsync(home, ct)
                ?? throw new Aor.ValidationException(Lingua("Settore Home inesistente.", "The Home sector does not exist."));

            // ⚠️ Una coppia, una vLOA. Il contratto di `FindVloaIdByPairAsync` lo dichiarava già — «una sola
            // vLOA per coppia ACC↔ACC» — e nessuno lo imponeva: la generazione da /services/vsop/admin/neighbours è
            // idempotente per parti, questa porta no. Le due strade per creare la stessa cosa avevano due
            // politiche diverse, e il resto dell'applicazione non sa gestirne due: `FindVloaIdByPairAsync`
            // fa `FirstOrDefault`, quindi con due documenti sulla stessa coppia l'editor ne apre uno **senza
            // un criterio** e l'altro resta invisibile — pur potendo avere release pubblicate.
            //
            // Non si «riusa in silenzio» come fa l'import: lì non c'è nessuno davanti, qui sì, e chi ha
            // appena scritto un titolo deve sapere perché non è stato usato.
            var neighAcc = await _repo.GetAccCodeBySectorAsync(neigh, ct)
                ?? throw new Aor.ValidationException(Lingua("Settore Neighbour inesistente.", "The Neighbour sector does not exist."));
            if (await _repo.FindVloaIdByPairAsync(accCode, neighAcc, ct) is int gia)
                throw new Aor.ValidationException(Lingua(
                    $"Esiste già una vLOA {accCode} ↔ {neighAcc} (documento #{gia}): aprila invece di crearne una seconda.",
                    $"A vLOA {accCode} ↔ {neighAcc} already exists (document #{gia}): open that one instead of creating a second."));
        }
        else
        {
            if (scopeSectorIds is null || scopeSectorIds.Count == 0)
                throw new Aor.ValidationException(Lingua("La vIPI richiede almeno un settore di scope.", "A vIPI needs at least one sector in scope."));
            scope = scopeSectorIds.Distinct().ToList();
            primary = primarySectorId ?? scope[0];
            if (!scope.Contains(primary.Value))
                throw new Aor.ValidationException(Lingua("Il settore primario deve far parte dello scope.", "The primary sector has to be part of the scope."));
            accCode = await _repo.GetAccCodeBySectorAsync(primary.Value, ct)
                ?? throw new Aor.ValidationException(Lingua("Settore di scope inesistente.", "The scope sector does not exist."));
        }

        _authz.EnsureAtLeast(VipiRole.Editor);
        var language = type == DocumentType.Vloa ? Language.En : Language.It;
        return await _repo.CreateDocumentAsync(type, title, language, scope, primary, parties, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task<int> CreateDraftAsync(int documentId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        // Creare/aprire una bozza = iniziare a editare → acquisisce (o conferma) il lock.
        var lk = await _repo.AcquireOrInspectLockAsync(documentId, _authz.CurrentUserId ?? 0, _authz.CurrentName, LockTtlMinutes, ct);
        if (!lk.IsMine) throw LockedByOther(lk);
        return await _repo.CreateDraftAsync(documentId, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default)
    {
        var docId = await AuthorizeBlockAsync(blockId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.UpdateBlockAsync(blockId, edit, ct);
    }

    public async Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        return await _repo.AddBlockAsync(sectionId, format, tier, visibility, ct);
    }

    public async Task DeleteBlockAsync(int blockId, CancellationToken ct = default)
    {
        var docId = await AuthorizeBlockAsync(blockId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.DeleteBlockAsync(blockId, ct);
    }

    public async Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.RenameSectionAsync(sectionId, title, ct);
    }

    public async Task SetSectionRenderModeAsync(int sectionId, RenderMode mode, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionRenderModeAsync(sectionId, mode, ct);
    }

    public async Task SetSectionAudienceAsync(int sectionId, SectionAudience audience, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionAudienceAsync(sectionId, audience, ct);
    }

    public async Task SetSectionBeforeParentBodyAsync(int sectionId, bool before, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionBeforeParentBodyAsync(sectionId, before, ct);
    }

    public async Task SetSectionLeadSentenceAsync(int sectionId, bool lead, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionLeadSentenceAsync(sectionId, lead, ct);
    }

    public async Task SetSectionHiddenAsync(int sectionId, bool hidden, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.SetSectionHiddenAsync(sectionId, hidden, ct);
    }

    public async Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default)
    {
        var docId = await AuthorizeVersionAsync(versionId, ct);
        await EnsureLockAsync(docId, ct);
        return await _repo.AddSectionAsync(versionId, parentSectionId, title, kind, ct);
    }

    public async Task DeleteSectionAsync(int sectionId, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.DeleteSectionAsync(sectionId, ct);
    }

    public async Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.MoveSectionAsync(sectionId, direction, ct);
    }

    public async Task MoveSectionBeforeAsync(int sectionId, int? beforeSectionId, CancellationToken ct = default)
    {
        var docId = await AuthorizeSectionAsync(sectionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.MoveSectionBeforeAsync(sectionId, beforeSectionId, ct);
    }

    public async Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default)
    {
        var docId = await AuthorizeBlockAsync(blockId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.MoveBlockAsync(blockId, direction, ct);
    }

    public async Task PublishAsync(int versionId, string? note, CancellationToken ct = default)
    {
        var docId = await AuthorizeVersionAsync(versionId, ct);
        await EnsureLockAsync(docId, ct);
        await _repo.PublishAsync(versionId, _authz.CurrentUserId ?? 0, note, ct);
        // Retention versioni: dopo l'archiviazione della precedente (in _repo.PublishAsync) → cap Archived esatto, non
        // N+1. Le release non referenziano le versioni (portano la fotografia), quindi potare è sicuro. Stessa regola del
        // release-publish (ReleaseService.PublishNowAsync) e del boot sweep.
        await _repo.PruneArchivedVersionsAsync(docId, _retention.KeepArchivedVersionsPerDocument, ct);
        await _repo.ReleaseLockAsync(docId, _authz.CurrentUserId ?? 0, ct); // pubblicato → lascia il documento libero
    }

    public async Task<int> DiscardDraftAsync(int versionId, CancellationToken ct = default)
    {
        var docId = await AuthorizeVersionAsync(versionId, ct);
        await EnsureLockAsync(docId, ct);

        var versions = await _repo.ListVersionsAsync(docId, ct);
        var draft = versions.FirstOrDefault(v => v.Id == versionId)
                    ?? throw new KeyNotFoundException($"Versione {versionId} inesistente.");

        // Si scarta una BOZZA, non una versione qualsiasi: pubblicate e archiviate sono storia del documento,
        // e cancellarle romperebbe ciò che le release dichiarano di aver fotografato.
        if (draft.Status != DocumentStatus.Draft)
            throw new ValidationException(Lingua($"La versione {draft.VersionNumber} non è una bozza ({draft.Status}): non si scarta.", $"Version {draft.VersionNumber} is not a draft ({draft.Status}): it cannot be discarded."));

        // Serve qualcosa a cui tornare. Su un documento mai pubblicato la bozza È il documento: scartarla
        // lascerebbe un guscio senza contenuto e senza vista pubblica — chi vuole disfarsene elimini il
        // documento, che è un'altra azione con altre conseguenze.
        if (!versions.Any(v => v.Id != versionId && v.Status is DocumentStatus.Published or DocumentStatus.Archived))
            throw new ValidationException(Lingua(
                "Questa bozza è l'unica versione del documento: scartandola non resterebbe nulla da mostrare. " +
                "Pubblicala, oppure elimina il documento.",
                "This draft is the document's only version: discarding it would leave nothing to show. " +
                "Publish it, or delete the document."));

        var numero = await _repo.DiscardDraftAsync(versionId, _authz.CurrentUserId ?? 0, ct);
        // Scartare è finire di editare: come la pubblicazione, lascia il documento libero per gli altri.
        await _repo.ReleaseLockAsync(docId, _authz.CurrentUserId ?? 0, ct);
        return numero;
    }

    // --- Lock ---
    public async Task<LockInfo> AcquireLockAsync(int documentId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        return await _repo.AcquireOrInspectLockAsync(documentId, _authz.CurrentUserId ?? 0, _authz.CurrentName, LockTtlMinutes, ct);
    }

    public Task<LockInfo> InspectLockAsync(int documentId, CancellationToken ct = default) =>
        _repo.InspectLockAsync(documentId, _authz.CurrentUserId ?? 0, ct);

    public async Task ReleaseLockAsync(int documentId, CancellationToken ct = default) =>
        await _repo.ReleaseLockAsync(documentId, _authz.CurrentUserId ?? 0, ct);

    public async Task ForceUnlockAsync(int documentId, CancellationToken ct = default)
    {
        // Togliere il lock a un collega è un atto forte, ma è lo stesso mestiere: chi può scrivere quel
        // documento può anche sbloccarlo. Prima serviva l'admin, quando l'admin era l'unico che editava.
        _authz.EnsureAtLeast(VipiRole.Editor);
        await _repo.ForceUnlockAsync(documentId, _authz.CurrentUserId ?? 0, ct);
    }

    /// <summary>Le mutazioni richiedono che il UserId corrente detenga il lock; rinnova la scadenza (sliding).</summary>
    private async Task EnsureLockAsync(int documentId, CancellationToken ct)
    {
        if (!await _repo.IsLockHeldByAsync(documentId, _authz.CurrentUserId ?? 0, ct))
            throw new EditConflictException(Lingua("Documento bloccato da un altro editor o lock scaduto: riapri l'editor per riacquisirlo.", "The document is locked by another editor, or the lock has expired: reopen the editor to take it again."));
        await _repo.RenewLockAsync(documentId, _authz.CurrentUserId ?? 0, LockTtlMinutes, ct);
    }

    private static EditConflictException LockedByOther(LockInfo lk) =>
        new($"In modifica da VID {lk.ByUserId} ({lk.ByName}) fino alle {lk.ExpiresUtc:HH:mm} UTC.");

    // --- Risoluzione del documento proprietario per l'autorizzazione ACC-scoped (ritorna il documentId) ---
    private async Task<int> AuthorizeVersionAsync(int versionId, CancellationToken ct)
    {
        var docId = await _repo.GetDocumentIdByVersionAsync(versionId, ct) ?? throw new EditNotAllowedException();
        _authz.EnsureAtLeast(VipiRole.Editor);
        return docId;
    }

    private async Task<int> AuthorizeSectionAsync(int sectionId, CancellationToken ct)
    {
        var docId = await _repo.GetDocumentIdBySectionAsync(sectionId, ct) ?? throw new EditNotAllowedException();
        _authz.EnsureAtLeast(VipiRole.Editor);
        return docId;
    }

    private async Task<int> AuthorizeBlockAsync(int blockId, CancellationToken ct)
    {
        var docId = await _repo.GetDocumentIdByBlockAsync(blockId, ct) ?? throw new EditNotAllowedException();
        _authz.EnsureAtLeast(VipiRole.Editor);
        return docId;
    }
}
