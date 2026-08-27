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
    /// snapshot ne assembla i blocchi: è la stessa fotografia, letta con l'attrezzo del suo tipo. Authz ACC.
    /// <para>
    /// ⚠️ Il bersaglio atteso è OBBLIGATORIO, e la firma è fatta apposta perché non si possa soddisfare senza
    /// dirlo (doc 14 §3a). Autorizzare chi guarda non basta: <c>?as=rel:57</c> su un URL dice «mostrami la
    /// release 57», non «mostrami la release 57 <b>di questo documento</b>», e chi può pubblicare due APP può
    /// pubblicare la release dell'uno sotto l'indirizzo dell'altro. Il confronto stava in TRE copie byte per
    /// byte nelle pagine e in una quarta forma dentro <c>AccDocumentService</c>: quattro posti in cui poteva
    /// mancare, e un quinto — la pagina successiva — in cui sarebbe mancato.
    /// </para>
    /// <para>Ritorna null — non solleva — se la release non esiste, se non è di quel bersaglio, o se non se ne
    /// ha il diritto: per una pagina i tre casi hanno lo stesso esito, ricadere sulla vista pubblica.</para>
    /// </summary>
    /// <param name="expectedType">Tipo del documento che sta chiedendo l'anteprima.</param>
    /// <param name="expectedKey">Chiave di release di quel documento (ICAO, callsign APP, id vLOA, «ACC|root»).</param>
    Task<ReleasePreview?> GetPreviewAsync(int releaseId, ReleaseTargetType expectedType, string expectedKey,
        CancellationToken ct = default);

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

    /// <summary>
    /// <b>Deriva</b>: che cosa direbbe oggi la copia pubblicata se la si rifacesse adesso, confrontato con
    /// quella in vigore. Vuoto = la release dice ancora il vero (o non c'è una release in vigore, e allora
    /// non c'è niente da cui derivare). Operazione di sistema: nessuna autorizzazione, la chiama un giro.
    ///
    /// <para>⚠️ Il confronto è quello del <c>Diff</c> fra release — voce (sezione/blocco) → conteggio degli
    /// elementi — quindi vede una sezione che cambia numero di righe, <b>non</b> un testo riscritto dentro
    /// una riga esistente. È un limite dichiarato: la casella deve promettere quel che misura.</para>
    /// </summary>
    Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

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
        IReleaseTargetRegistry targets, IOptions<ReleaseRetentionOptions> retention, IUnitOfWork uow,
        ShapeReleaseContext? shapeCycle = null,
        Abstractions.ITranslationMemory? memoriaTraduzioni = null,
        IOptions<Translation.TranslationOptions>? traduzione = null)
    {
        _shapeCycle = shapeCycle;
        _memoriaTraduzioni = memoriaTraduzioni;
        _traduzione = traduzione?.Value;
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

    /// <summary>Il contesto che dice alla lettura delle shape «sto congelando per questo ciclo». Opzionale:
    /// senza, il congelamento prende le geometrie correnti — cioè il comportamento di prima del gate.</summary>
    private readonly ShapeReleaseContext? _shapeCycle;

    /// <summary>La memoria di traduzione. Opzionale: senza, la release non congela traduzioni e il viewer
    /// ricade sulla memoria viva — che e' il comportamento di prima di questa funzione.</summary>
    private readonly Abstractions.ITranslationMemory? _memoriaTraduzioni;

    private readonly Translation.TranslationOptions? _traduzione;

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

    public async Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(
        ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        var effettiva = await _repo.GetEffectiveAsync(type, key, DateTime.UtcNow, ct);
        if (effettiva is null) return Array.Empty<ReleaseDiffRow>();

        // Lo snapshot che si otterrebbe pubblicando ADESSO. Stesso identico percorso della pubblicazione
        // vera (§3d): se divergessero, la deriva segnalerebbe differenze che al momento di pubblicare non
        // esistono — o, peggio, tacerebbe su quelle che esistono.
        var oggiJson = await BuildSnapshotJsonAsync(type, key, _airac.GetCycle(DateTime.UtcNow), ct);
        if (oggiJson is null) return Array.Empty<ReleaseDiffRow>();

        var oggi = Signature(oggiJson);
        var pubblicata = Signature(effettiva.PayloadJson);

        var righe = new List<ReleaseDiffRow>();
        foreach (var kv in oggi.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!pubblicata.TryGetValue(kv.Key, out var p))
                righe.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Added, null, kv.Value));
            else if (p != kv.Value)
                righe.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Modified, p, kv.Value));
        }
        foreach (var kv in pubblicata.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            if (!oggi.ContainsKey(kv.Key))
                righe.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Removed, kv.Value, null));

        return righe;
    }

    public async Task<ReleasePreview?> GetPreviewAsync(int releaseId, ReleaseTargetType expectedType,
        string expectedKey, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct);
        if (rel is null) return null;

        // La release deve essere DI QUESTO documento. Prima di questo controllo, chi poteva editare due APP
        // poteva farsi mostrare l'uno sotto l'indirizzo dell'altro — con l'intestazione della pagina sbagliata.
        if (rel.TargetType != expectedType
            || !string.Equals(rel.TargetKey, expectedKey, StringComparison.OrdinalIgnoreCase))
            return null;

        // Chi non può vedere l'anteprima non riceve un'eccezione ma un null: per una PAGINA «non ne hai il
        // diritto» e «non c'è» hanno lo stesso esito — si ricade sulla vista pubblica — e infatti tutte e tre
        // le pagine che chiamavano questo metodo avvolgevano la chiamata negli stessi due catch. Erano la
        // metà mancante della guardia: tenerli fuori voleva dire che una pagina nuova poteva scordarsi anche
        // di quelli e far cadere il circuito addosso a un lettore anonimo.
        try { await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct); }
        catch (EditNotAllowedException) { return null; }
        catch (Aor.ValidationException) { return null; }

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

        // ⚠️ Il congelamento chiede le shape IN VIGORE AL CICLO DI QUESTA RELEASE, non le più recenti: il
        // sectorfile lo scriviamo in anticipo, quindi in catalogo può già esserci il confine del ciclo
        // prossimo. Pubblicando per il ciclo corrente esce la geometria vecchia; pubblicando in anticipo
        // PER il ciclo prossimo — che è quel che si fa preparando un AIRAC — esce quella nuova. Vedi
        // ShapeAiracGate e docs/feature/2026-08-26-shape-dal-sectorfile.md §3.
        IReadOnlyDictionary<int, string> frozen;
        using (_shapeCycle?.Capturing(cycle))
            frozen = await _frozen.CaptureAsync(type, key, payload.Doc, ct);
        foreach (var kv in frozen) payload.FrozenSections[kv.Key] = kv.Value;

        payload.Doc = await ConTraduzioniCongelateAsync(payload.Doc, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Copia nello snapshot le traduzioni note per i segmenti di QUESTO documento (carta bilingue §6).
    ///
    /// <para>
    /// ⚠️ <b>Congelare non e' cautela, e' l'unico modo di limitare il raggio d'azione di una correzione.</b>
    /// La memoria e' indicizzata sulla FRASE: senza questa fotografia, chi corregge una resa su un documento
    /// cambierebbe l'inglese gia' pubblicato di ogni altro documento che contiene quella frase — sotto gli
    /// occhi di chi lo sta leggendo, e senza che il suo editor abbia pubblicato niente. Congelata, la
    /// correzione arriva agli altri alla LORO prossima ripubblicazione, quando il loro editor vede il diff.
    /// </para>
    /// <para>
    /// Senza memoria configurata o senza lingua sorgente nota, torna il documento intatto: il viewer ricadra'
    /// sulla memoria viva, che e' il comportamento di prima di questa funzione.
    /// </para>
    /// </summary>
    private async Task<RawDocument> ConTraduzioniCongelateAsync(RawDocument raw, CancellationToken ct)
    {
        if (_memoriaTraduzioni is null || _traduzione is null || !_traduzione.Enabled) return raw;
        if (raw.Language is not { } sorgente) return raw;

        var da = sorgente == Vipi.Domain.Language.En ? "en" : "it";
        var segmenti = SegmentiDi(raw).Distinct(StringComparer.Ordinal).ToList();
        if (segmenti.Count == 0) return raw;

        var impronte = segmenti.Select(Translation.TranslationText.Hash).Distinct().ToList();
        var congelate = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in _traduzione.Targets)
        {
            if (string.Equals(a, da, StringComparison.OrdinalIgnoreCase)) continue;
            var note = await _memoriaTraduzioni.LookupAsync(da, a, impronte, ct).ConfigureAwait(false);
            if (note.Count == 0) continue;
            congelate[a] = note.ToDictionary(kv => kv.Key, kv => kv.Value.TargetText, StringComparer.Ordinal);
        }

        if (congelate.Count == 0) return raw;

        return new RawDocument
        {
            Title = raw.Title,
            AiracCycle = raw.AiracCycle,
            Roots = raw.Roots,
            Language = raw.Language,
            Translations = congelate,
        };
    }

    /// <summary>Ogni testo traducibile dello snapshot: titoli di sezione, paragrafi e celle dei blocchi.</summary>
    private static IEnumerable<string> SegmentiDi(RawDocument raw)
    {
        var titolo = Translation.TranslationText.Normalize(raw.Title);
        if (Translation.TranslationText.HasSomethingToTranslate(titolo)) yield return titolo;

        foreach (var s in raw.Roots)
            foreach (var t in SegmentiDiSezione(s))
                yield return t;
    }

    private static IEnumerable<string> SegmentiDiSezione(RawSection s)
    {
        var titolo = Translation.TranslationText.Normalize(s.Title);
        if (Translation.TranslationText.HasSomethingToTranslate(titolo)) yield return titolo;

        foreach (var b in s.Blocks)
        {
            foreach (var p in Translation.TextSegmenter.SplitProse(b.Body))
                if (Translation.TranslationText.HasSomethingToTranslate(p)) yield return p;

            foreach (var c in Translation.TextSegmenter.SplitJson(b.BodyJson))
            {
                var norm = Translation.TranslationText.Normalize(c);
                if (Translation.TranslationText.HasSomethingToTranslate(norm)) yield return norm;
            }
        }

        foreach (var figlia in s.Children)
            foreach (var t in SegmentiDiSezione(figlia))
                yield return t;
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
