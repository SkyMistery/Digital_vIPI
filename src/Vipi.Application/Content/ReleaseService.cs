using System.Text.Json;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case delle release AIRAC: pubblica lo snapshot editoriale del documento a un ciclo di rilascio (schedulato o
/// immediato), elenca la timeline. Lo stato live resta la bozza; la release ne congela una fotografia visibile al
/// pubblico dal ciclo di rilascio in poi. Scritture gated via authz ACC.
/// </summary>
public interface IReleaseService
{
    Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>Pubblica lo snapshot corrente al ciclo AIRAC indicato (entra in vigore alla sua data efficace).
    /// Rifiuta se il documento è lockato da un altro editor: lo snapshot fotografa la sua bozza in lavorazione.</summary>
    Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default);

    /// <summary>Forza la pubblicazione immediata (review): ciclo corrente, effettiva adesso. Rifiuta se il documento
    /// è lockato da un altro editor (promuoverebbe la sua bozza a metà); a pubblicazione avvenuta rilascia
    /// l'eventuale lock del chiamante, come il publish-versione dell'editor.</summary>
    Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default);

    /// <summary>Migrazione A (doc 10 §3f): per ogni documento <c>Published</c> e non nascosto SENZA release effettiva,
    /// genera una copia statica al ciclo corrente (effettiva adesso), così togliere il fallback live pubblico (S6b) non
    /// lascia buchi. Operazione di sistema (nessuna authz), idempotente: salta i bersagli già coperti e i documenti
    /// senza contenuto. Ritorna il numero di release generate.</summary>
    Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default);

    /// <summary>Annulla una release (per Id). Authz sull'ACC del bersaglio.</summary>
    Task CancelReleaseAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Riepilogo differenze di una release rispetto a quella immediatamente PRECEDENTE nella storia del
    /// bersaglio (ordine: data efficace, poi progressivo) — «cosa ha cambiato questa pubblicazione». Nessuna
    /// precedente = prima pubblicazione (tutte le voci «Aggiunta»). Authz ACC, come Preview/Location.</summary>
    Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Anteprima di una release: metadati + <see cref="RawDocument"/> del payload. Vale per TUTTI i tipi —
    /// dal doc 08 condividono <c>DocReleasePayload</c> — e non solo per vLOA/aeroporto come diceva questo commento.
    /// La vIPI ACC ha comunque una porta propria (<c>IAccDocumentService.LoadForReleaseAsync</c>), che oltre allo
    /// snapshot ne assembla i blocchi: è la stessa fotografia, letta con l'attrezzo del suo tipo. Authz ACC.</summary>
    Task<ReleasePreview?> GetPreviewAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Identità (tipo/chiave/ciclo/ACC) di una release, per risolvere la route del viewer tipizzato.
    /// Authz ACC come le altre operazioni di release. null se inesistente.</summary>
    Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Ciclo AIRAC corrente.</summary>
    string CurrentCycle();

    /// <summary>I prossimi <paramref name="count"/> cicli AIRAC (corrente incluso), per il selettore di rilascio.</summary>
    IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count);

    /// <summary>Riepilogo release (in vigore / prossima schedulata) per un insieme di bersagli, in un'unica query,
    /// per mostrare lo stato sulle righe collassate dell'elenco. Chiave = (TargetType, TargetKey).</summary>
    Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
        IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default);

    /// <summary>Sweep di retention su tutti i documenti gestiti (system op, come <see cref="BackfillMissingReleasesAsync"/>):
    /// pota release Superseded oltre soglia e versioni Archived oltre N per ciascun bersaglio. Idempotente. Ritorna il
    /// numero di versioni archiviate rimosse.</summary>
    Task<int> PruneAllAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IReleaseService"/>
public sealed class ReleaseService : IReleaseService
{
    private readonly IReleaseRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IAiracService _airac;
    private readonly IFrozenSectionRegistry _frozen;
    private readonly IDocumentAdminRepository _admin;
    private readonly IEditingRepository _editing;
    private readonly IReleaseTargetRegistry _targets;
    private readonly ReleaseRetentionOptions _retention;
    private readonly IUnitOfWork _uow;

    public ReleaseService(IReleaseRepository repo, IEditAuthorizationService authz, IAiracService airac,
        IFrozenSectionRegistry frozen, IDocumentAdminRepository admin, IEditingRepository editing,
        IReleaseTargetRegistry targets, IOptions<ReleaseRetentionOptions> retention, IUnitOfWork uow)
    {
        _repo = repo;
        _authz = authz;
        _airac = airac;
        _frozen = frozen;
        _admin = admin;
        _editing = editing;
        _targets = targets;
        _retention = retention.Value;
        _uow = uow;
    }

    public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
        _repo.ListAsync(type, key, ct);

    public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
        IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
        _repo.SummariesAsync(targets, ct);

    public string CurrentCycle() => _airac.GetCycle(DateTime.UtcNow);

    // Parte dal ciclo SUCCESSIVO: il corrente si pubblica con "Pubblica ora". Salta il primo (corrente) di NextCycles.
    public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) =>
        _airac.NextCycles(DateTime.UtcNow, count + 1).Skip(1).ToList();

    public async Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(type, key, ct);
        await EnsureNotLockedByOthersAsync(type, key, ct);
        var effectiveUtc = _airac.EffectiveUtcForCycle(releaseCycle);
        await SnapshotAndSaveAsync(type, key, releaseCycle, effectiveUtc, note, ct);
    }

    /// <summary>
    /// Pubblicazione immediata. <b>Tutta dentro una transazione</b>: sono tre scritture distinte, e uno stato
    /// intermedio committato è incoerente in modo vistoso — una release pubblicata di un documento la cui
    /// bozza non è stata promossa, cioè la pagina pubblica che mostra il nuovo e l'editor che mostra il
    /// vecchio. È l'operazione più importante che l'applicazione compie, ed era l'unica senza rete.
    ///
    /// <para><see cref="IUnitOfWork"/> esisteva già ed era usato in due soli posti. Si occupa anche del caso
    /// spinoso: su Neon la strategia di retry rifiuta le transazioni aperte a mano, quindi il blocco va
    /// dentro <c>CreateExecutionStrategy</c> — e al retry il change-tracker va azzerato, o le entità del
    /// tentativo fallito rientrano insieme a quelle del nuovo. Entrambe le cose sono in <c>EfUnitOfWork</c>.</para>
    ///
    /// <para>⚠️ L'autorizzazione resta <b>fuori</b> dalla transazione: negare un permesso non è una scrittura
    /// da annullare, e tenerla dentro significherebbe aprire una transazione anche per rifiutare. Fuori sta anche
    /// il controllo del lock (<see cref="EnsureNotLockedByOthersAsync"/>), per la stessa ragione.</para>
    /// </summary>
    public async Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(type, key, ct);
        var docId = await EnsureNotLockedByOthersAsync(type, key, ct);
        var now = DateTime.UtcNow;
        var cycle = _airac.GetCycle(now);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await SnapshotAndSaveAsync(type, key, cycle, now, note, token);
            // Pubblicazione IMMEDIATA (review): promuove anche la bozza a versione pubblicata, così lo stato del
            // documento e quello della release restano allineati (la pill dell'editor, la storia versioni, il diff).
            // La VISIBILITÀ pubblica non dipende più da questo: dal doc 10 §S6b è la release effettiva a decidere, e
            // il fallback live del viewer non c'è più — questo commento diceva ancora il contrario.
            // Le release SCHEDULATE (PublishAsync, ciclo futuro) NON promuovono: restano solo snapshot per il ciclo.
            await _repo.PublishWorkingVersionAsync(type, key, _authz.CurrentUserId ?? 0, cycle, token);
            // Retention versioni: DOPO la promozione (che archivia la precedente), così il conteggio Archived include la
            // versione appena archiviata → cap esatto, non N+1. Lo scheduled non promuove/archivia → non serve qui.
            await PruneArchivedVersionsForTargetAsync(type, key, token);
        }, ct);

        // Pubblicato → il documento resta libero, come dopo il publish-versione dell'editor
        // (EditingService.PublishAsync). ReleaseLockAsync è no-op se il lock non è del chiamante.
        if (docId is int id)
            await _editing.ReleaseLockAsync(id, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var cycle = _airac.GetCycle(now);
        var count = 0;
        foreach (var d in await _admin.ListAsync(ct))
        {
            if (!d.IsPublished || d.IsHidden) continue;   // solo i pubblicati, non nascosti
            if (await _repo.GetEffectiveAsync(d.ReleaseTarget, d.ReleaseKey, now, ct) is not null) continue;   // già coperto → idempotente

            // Riusa il path di cattura (§3d); tollera i documenti senza contenuto (null) senza esplodere.
            var finalJson = await BuildSnapshotJsonAsync(d.ReleaseTarget, d.ReleaseKey, cycle, ct);
            if (finalJson is null) continue;
            await _repo.SaveReleaseAsync(d.ReleaseTarget, d.ReleaseKey, cycle, now,
                finalJson, createdByUserId: 0, note: "backfill migrazione A (doc 10)", ct);
            count++;
        }
        return count;
    }

    public async Task CancelReleaseAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct)
            ?? throw new Aor.ValidationException("Release inesistente.");
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);
        await _repo.CancelAsync(releaseId, ct);
    }

    public async Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct);
        if (rel is null) return ReleaseDiff.Empty;
        // Stessa authz delle altre letture di release (Preview/Location): il diff espone titoli e struttura.
        // ReleasePanel e VersioniPage catturavano già EditNotAllowedException attorno a questa chiamata —
        // una cattura che non poteva scattare, perché il gate qui mancava.
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);

        // Baseline = la release immediatamente PRECEDENTE nella storia del bersaglio (data efficace, poi
        // progressivo): il diff risponde «cosa ha cambiato QUESTA pubblicazione». Prima la baseline era
        // «l'effettiva ORA, esclusa quella in esame»: proprio per la release in vigore — il diff più
        // richiesto — diventava null, e la UI diceva «nessuna release in vigore» con tutte le sezioni
        // «Aggiunta» anche alla decima pubblicazione.
        var storia = await _repo.ListAsync(rel.TargetType, rel.TargetKey, ct);
        var precedente = storia
            .Where(r => r.ReleaseEffectiveUtc < rel.ReleaseEffectiveUtc
                        || (r.ReleaseEffectiveUtc == rel.ReleaseEffectiveUtc && r.VersionNumber < rel.VersionNumber))
            .OrderByDescending(r => r.ReleaseEffectiveUtc).ThenByDescending(r => r.VersionNumber)
            .FirstOrDefault();
        var baseline = precedente is null ? null : await _repo.GetByIdAsync(precedente.Id, ct);

        var cur = Signature(rel.PayloadJson);
        var prev = baseline is null ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                                    : Signature(baseline.PayloadJson);

        var rows = new List<ReleaseDiffRow>();
        foreach (var kv in cur.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!prev.TryGetValue(kv.Key, out var p))
                rows.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Added, null, kv.Value));
            else if (p != kv.Value)
                rows.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Modified, p, kv.Value));
        }
        foreach (var kv in prev.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            if (!cur.ContainsKey(kv.Key))
                rows.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Removed, kv.Value, null));

        // Niente frasi in Application: il ciclo di confronto (o la sua assenza) lo formatta la UI.
        return new ReleaseDiff(baseline is not null, baseline?.ReleaseAiracCycle, rows);
    }

    public async Task<ReleasePreview?> GetPreviewAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct);
        if (rel is null) return null;
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);

        // Post-08 tutti i tipi condividono DocReleasePayload → deserializzazione unica, nessuno switch per-tipo.
        RawDocument? doc = null;
        try { doc = JsonSerializer.Deserialize<DocReleasePayload>(rel.PayloadJson)?.Doc; }
        catch (JsonException) { }
        return new ReleasePreview(rel.TargetType, rel.TargetKey, rel.ReleaseAiracCycle, doc);
    }

    public async Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct);
        if (rel is null) return null;
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);
        var acc = await _repo.GetAuthAccCodeAsync(rel.TargetType, rel.TargetKey, ct)
            ?? throw new Aor.ValidationException("Bersaglio della release inesistente.");
        return new ReleaseLocation(rel.TargetType, rel.TargetKey, rel.ReleaseAiracCycle, acc);
    }

    /// <summary>Firma editoriale di un payload: voce (sezione/blocco) → conteggio elementi. Base del diff.
    /// Post-08 tutti i tipi sono su DocReleasePayload → firma unica, nessuno switch per-tipo.</summary>
    private static Dictionary<string, int> Signature(string payloadJson)
    {
        var sig = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var p = JsonSerializer.Deserialize<DocReleasePayload>(payloadJson);
            if (p?.Doc?.Roots is not null) FlattenSections(p.Doc.Roots, "", sig);
        }
        catch (JsonException) { }
        return sig;
    }

    private static void FlattenSections(IReadOnlyList<RawSection> sections, string prefix, Dictionary<string, int> sig)
    {
        foreach (var s in sections)
        {
            var label = prefix.Length == 0 ? s.Title : $"{prefix} / {s.Title}";
            sig[label] = s.Blocks.Count;
            if (s.Children.Count > 0) FlattenSections(s.Children, label, sig);
        }
    }

    private async Task SnapshotAndSaveAsync(ReleaseTargetType type, string key, string cycle, DateTime effectiveUtc, string? note, CancellationToken ct)
    {
        var finalJson = await BuildSnapshotJsonAsync(type, key, cycle, ct)
            ?? throw new Aor.ValidationException("Nessun contenuto da pubblicare: crea prima il documento (bozza).");
        await _repo.SaveReleaseAsync(type, key, cycle, effectiveUtc, finalJson, _authz.CurrentUserId ?? 0, note, ct);
        // Retention per-publish (release Superseded): sia per l'immediato sia per lo schedulato. Le versioni Archived
        // si potano solo dopo la promozione della bozza (PublishNowAsync) → vedi PruneArchivedVersionsForTargetAsync.
        await _repo.PruneReleasesAsync(type, key, KeepSupersededFromUtc(), ct);
    }

    public async Task<int> PruneAllAsync(CancellationToken ct = default)
    {
        var removed = 0;
        var keepFrom = KeepSupersededFromUtc();
        foreach (var d in await _admin.ListAsync(ct))
        {
            await _repo.PruneReleasesAsync(d.ReleaseTarget, d.ReleaseKey, keepFrom, ct);
            if (d.DocumentId is int docId)
                removed += await _editing.PruneArchivedVersionsAsync(docId, _retention.KeepArchivedVersionsPerDocument, ct);
        }
        return removed;
    }

    // Potatura versioni Archived oltre N del bersaglio. Va invocata DOPO l'archiviazione della versione appena
    // pubblicata (PublishNowAsync), altrimenti il conteggio è di uno in meno e resta N+1.
    private async Task PruneArchivedVersionsForTargetAsync(ReleaseTargetType type, string key, CancellationToken ct)
    {
        var docId = await _targets.For(type).ResolveDocumentIdAsync(key, ct);
        if (docId is int id)
            await _editing.PruneArchivedVersionsAsync(id, _retention.KeepArchivedVersionsPerDocument, ct);
    }

    // Soglia temporale: data efficace del ciclo AIRAC corrente meno N cicli (28 giorni cadauno). Le release Superseded
    // con data efficace anteriore vengono potate.
    private DateTime KeepSupersededFromUtc() =>
        _airac.EffectiveUtcForCycle(_airac.GetCycle(DateTime.UtcNow))
              .AddDays(-_retention.KeepSupersededWithinCycles * 28);

    // Snapshot totale (doc 10 §3c): struttura congelata + OUTPUT delle sezioni derivate in modalità Frozen, così il
    // pubblico vede una fotografia completa (le sezioni Live restano fuori: il viewer le deriva sul momento). Ritorna il
    // JSON del payload pronto per SaveReleaseAsync, o null se il documento non ha contenuto (nessuna versione di lavoro).
    private async Task<string?> BuildSnapshotJsonAsync(ReleaseTargetType type, string key, string cycle, CancellationToken ct)
    {
        var json = await _repo.SnapshotWorkingAsync(type, key, cycle, ct);
        if (json is null) return null;
        var payload = JsonSerializer.Deserialize<DocReleasePayload>(json)!;
        var frozen = await _frozen.CaptureAsync(type, key, payload.Doc, ct);
        foreach (var kv in frozen) payload.FrozenSections[kv.Key] = kv.Value;
        return JsonSerializer.Serialize(payload);
    }

    private async Task EnsureCanEditAsync(ReleaseTargetType type, string key, CancellationToken ct)
    {
        var acc = await _repo.GetAuthAccCodeAsync(type, key, ct)
            ?? throw new Aor.ValidationException("Bersaglio della release inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }

    /// <summary>
    /// Il lock di editing vale anche per le release: lo snapshot fotografa la BOZZA (WorkingVersionIdAsync), e
    /// «Pubblica ora» la promuove pure — pubblicare mentre un altro editor la sta scrivendo congela il suo lavoro
    /// a metà e gli rompe la sessione (la bozza diventa Published e i salvataggi successivi vengono rifiutati).
    /// Il publish-versione dell'editor (EditingService.PublishAsync) il lock lo pretende; qui basta il guard
    /// inverso — nessun ALTRO lo detiene — perché il pannello release non ha il ciclo di vita del lock.
    /// Ritorna il documentId risolto (null se il bersaglio non ha Document), così PublishNow può liberarlo dopo.
    /// </summary>
    private async Task<int?> EnsureNotLockedByOthersAsync(ReleaseTargetType type, string key, CancellationToken ct)
    {
        var docId = await _targets.For(type).ResolveDocumentIdAsync(key, ct);
        if (docId is not int id) return null;
        var lk = await _editing.InspectLockAsync(id, _authz.CurrentUserId ?? 0, ct);
        if (lk.Locked && !lk.IsMine)
            throw new Aor.ValidationException(
                $"Documento in modifica da VID {lk.ByUserId} ({lk.ByName}) fino alle {lk.ExpiresUtc:HH:mm} UTC: la release fotografa la sua bozza, riprova quando ha finito.");
        return id;
    }
}
