using System.Text.Json;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

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

    /// <summary>
    /// I bersagli che una pubblicazione di questo documento tocca: <b>lui solo</b> se non è unito, oppure
    /// <b>tutti i membri</b> dell'unione, nell'ordine (carta
    /// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §6).
    ///
    /// <para>Serve a <b>dirlo prima</b>: il pannello mostra i membri e chi ne tiene il lock, e chi preme sa
    /// quanti documenti sta pubblicando. ⚠️ Un esito che tace metà del lavoro è peggio di nessun esito —
    /// lezione già pagata con l'«auto-assegna» degli aeroporti.</para>
    /// </summary>
    Task<IReadOnlyList<BersaglioUnito>> BersagliUnitiAsync(ReleaseTargetType type, string key,
                                                           CancellationToken ct = default);

    /// <summary>
    /// <inheritdoc cref="PublishAsync" path="/summary"/> Per <b>tutti</b> i membri dell'unione, se questo
    /// documento è unito; altrimenti è esattamente <see cref="PublishAsync"/>.
    ///
    /// <para>⚠️ <b>Tutto o niente, in una transazione sola.</b> <c>SaveReleaseAsync</c> fa un
    /// <c>SaveChanges</c> per chiamata e <c>VersionNumber</c> è <c>max+1</c> letto in memoria sotto un
    /// indice UNICO: due salvataggi in fila non sono atomici, e un secondo membro che collide lascerebbe il
    /// primo pubblicato da solo — metà unione a un ciclo e metà a un altro.</para>
    ///
    /// <para>⚠️ Il ciclo è lo stesso per costruzione: la data efficace la calcola
    /// <c>AiracService.EffectiveUtcForCycle</c> dal ciclo passato, quindi i membri escono con la stessa
    /// senza doverla copiare a mano. È questo che fa funzionare anche la pianificata.</para>
    /// </summary>
    Task PublishUnionAsync(ReleaseTargetType type, string key, string releaseCycle, string? note,
                           CancellationToken ct = default);

    /// <summary><inheritdoc cref="PublishNowAsync" path="/summary/para[1]"/> Per <b>tutti</b> i membri
    /// dell'unione, se questo documento è unito; altrimenti è esattamente <see cref="PublishNowAsync"/>.
    /// <para>⚠️ Le due semantiche restano diverse anche unite: la pianificata <b>non</b> promuove la bozza,
    /// la «pubblica ora» sì — per ogni membro.</para></summary>
    Task PublishUnionNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default);

    /// <summary>Forza la pubblicazione immediata (review): ciclo corrente, effettiva adesso. Rifiuta se il documento
    /// è lockato da un altro editor (promuoverebbe la sua bozza a metà); a pubblicazione avvenuta rilascia
    /// l'eventuale lock del chiamante, come il publish-versione dell'editor.</summary>
    Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default);

    /// <summary>Migrazione A (doc 10 §3f): per ogni documento <c>Published</c> e non nascosto SENZA release effettiva,
    /// genera una copia statica al ciclo corrente (effettiva adesso), così togliere il fallback live pubblico (S6b) non
    /// lascia buchi. Operazione di sistema (nessuna authz), idempotente: salta i bersagli già coperti e i documenti
    /// senza contenuto. Ritorna il numero di release generate.</summary>
    Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default);

    /// <summary>
    /// Annulla una release (per Id). Authz sull'ACC del bersaglio.
    ///
    /// <para>⚠️ <b>Su un documento UNITO annulla anche le sorelle dello stesso ciclo</b>, nella stessa
    /// transazione. È il simmetrico della pubblicazione accoppiata: annullarne una sola lascerebbe metà
    /// unione in vigore a quel ciclo e metà no, cioè esattamente la desincronizzazione che l'accoppiamento
    /// doveva togliere — e la pagina unita mostrerebbe due fotografie di momenti diversi senza dirlo.</para>
    /// </summary>
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
    /// <param name="alCiclo">
    /// A che ciclo AIRAC si guarda. <c>null</c> = quello corrente, cioè «adesso».
    /// <para>⚠️ Serve per guardare al <b>ciclo entrante</b> (carta 2026-09-02 §AW1). Le derivate che
    /// dipendono dal ciclo — le SID d'aeroporto, le shape dei settori — <b>nascondono</b> quel che entra
    /// dopo: chiedendo sempre il ciclo di oggi, il giro della deriva non poteva vedere quel che sta per
    /// cambiare, e la riga «da ripubblicare» arrivava sempre <b>un ciclo tardi</b>, cioè il giorno dopo il
    /// rollover, a ciclo già in vigore.</para>
    /// </param>
    Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(ReleaseTargetType type, string key,
        string? alCiclo = null, CancellationToken ct = default);

    /// <summary>Il ciclo AIRAC <b>entrante</b> con la sua data efficace: il primo che non è ancora in vigore.</summary>
    AiracCycleInfo NextCycle();

    /// <summary>Sweep di retention su tutti i documenti gestiti (system op, come <see cref="BackfillMissingReleasesAsync"/>):
    /// pota release Superseded oltre soglia e versioni Archived oltre N per ciascun bersaglio. Idempotente. Ritorna il
    /// numero di versioni archiviate rimosse.</summary>
    Task<int> PruneAllAsync(CancellationToken ct = default);
}

/// <summary>
/// Un bersaglio che una pubblicazione tocca: il documento stesso, o un membro dell'unione a cui appartiene.
/// </summary>
/// <param name="Titolo">Come si chiama, per dirlo a chi sta per premere.</param>
/// <param name="LockedByUserId">Chi tiene il lock di modifica ATTIVO, se qualcuno. ⚠️ Con un lock altrui la
/// pubblicazione dell'INTERA unione si rifiuta: mezza unione pubblicata è peggio di nessuna.</param>
public sealed record BersaglioUnito(ReleaseTargetType Type, string Key, int DocumentId, string Titolo,
                                    int? LockedByUserId, string? LockedByName);

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

    /// <summary>Le sole RIGHE delle unioni. ⚠️ Non <c>IDocumentUnionService</c>: quello autorizza, e qui
    /// l'autorizzazione la fa già <see cref="EnsureCanEditAsync"/> per ogni membro — due cancelli sulla
    /// stessa porta sono due posti in cui possono dire cose diverse. Opzionale: senza, un documento non
    /// risulta mai unito, che è il comportamento di prima della carta.</summary>
    private readonly IDocumentUnionRepository? _unioni;

    public ReleaseService(IReleaseRepository repo, IEditAuthorizationService authz, IAiracService airac,
        IFrozenSectionRegistry frozen, IDocumentAdminRepository admin, IEditingRepository editing,
        IReleaseTargetRegistry targets, IOptions<ReleaseRetentionOptions> retention, IUnitOfWork uow,
        IDocumentUnionRepository? unioni = null,
        ShapeReleaseContext? shapeCycle = null,
        Abstractions.ITranslationMemory? memoriaTraduzioni = null,
        IOptions<Translation.TranslationOptions>? traduzione = null,
        ReadingLanguageContext? linguaProsa = null)
    {
        _linguaProsa = linguaProsa;
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
        _unioni = unioni;
    }

    /// <summary>Il contesto che dice alla lettura delle shape «sto congelando per questo ciclo». Opzionale:
    /// senza, il congelamento prende le geometrie correnti — cioè il comportamento di prima del gate.</summary>
    private readonly ShapeReleaseContext? _shapeCycle;

    /// <summary>La memoria di traduzione. Opzionale: senza, la release non congela traduzioni e il viewer
    /// ricade sulla memoria viva — che e' il comportamento di prima di questa funzione.</summary>
    private readonly Abstractions.ITranslationMemory? _memoriaTraduzioni;

    private readonly Translation.TranslationOptions? _traduzione;

    /// <summary>In che lingua comporre la prosa generata mentre si congela. Vedi BuildSnapshotJsonAsync.</summary>
    private readonly ReadingLanguageContext? _linguaProsa;

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

    // ---- L'unione: piu' documenti, un gesto solo (carta 2026-09-03) -----------------------------------

    public async Task<IReadOnlyList<BersaglioUnito>> BersagliUnitiAsync(
        ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        if (_unioni is null) return Array.Empty<BersaglioUnito>();

        var docId = await _targets.For(type).ResolveDocumentIdAsync(key, ct).ConfigureAwait(false);
        if (docId is null or 0) return Array.Empty<BersaglioUnito>();

        var righe = await _unioni.ByDocumentAsync(docId.Value, ct).ConfigureAwait(false);
        if (righe.Count == 0) return Array.Empty<BersaglioUnito>();

        // L'identita' dei membri con gli STESSI descrittori dell'elenco unificato, e il lock che arriva dalla
        // stessa query: nessuna interrogazione in piu' per sapere chi sta lavorando su cosa.
        var descritti = await _admin.DescribeAsync(righe.Select(r => r.DocumentId).ToList(), ct)
                                    .ConfigureAwait(false);
        return righe
            .OrderBy(r => r.Order)
            // ⚠️ Un membro che nessun descrittore riconosce si SALTA: pubblicare un bersaglio che non si sa
            // nominare vorrebbe dire scrivere una release sotto una chiave inventata.
            .Where(r => descritti.ContainsKey(r.DocumentId))
            .Select(r => descritti[r.DocumentId])
            .Select(d => new BersaglioUnito(d.ReleaseTarget, d.ReleaseKey, d.DocumentId!.Value, d.Title,
                                            d.LockedByUserId, d.LockedByName))
            .ToList();
    }

    public async Task PublishUnionAsync(ReleaseTargetType type, string key, string releaseCycle, string? note,
                                        CancellationToken ct = default)
    {
        var membri = await BersagliUnitiAsync(type, key, ct).ConfigureAwait(false);
        if (membri.Count == 0) { await PublishAsync(type, key, releaseCycle, note, ct).ConfigureAwait(false); return; }

        // ⚠️ I cancelli PRIMA, TUTTI, e fuori dalla transazione: un permesso negato o un lock altrui non
        // sono scritture da annullare, e scoprirli a meta' elenco vorrebbe dire aver gia' fotografato qualcuno.
        foreach (var m in membri)
        {
            await EnsureCanEditAsync(m.Type, m.Key, ct).ConfigureAwait(false);
            await EnsureNotLockedByOthersAsync(m.Type, m.Key, ct).ConfigureAwait(false);
        }

        var effectiveUtc = _airac.EffectiveUtcForCycle(releaseCycle);
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            // ⚠️ IN SEQUENZA, mai in parallelo: la cattura apre `ShapeReleaseContext.Capturing`, che NON e'
            // annidabile (il suo Dispose azzera), e `ReadingLanguageContext.Rendering` con la lingua sorgente
            // di QUEL membro. Due catture sovrapposte congelerebbero l'una nel contesto dell'altra.
            foreach (var m in membri)
                await SnapshotAndSaveAsync(m.Type, m.Key, releaseCycle, effectiveUtc, note, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task PublishUnionNowAsync(ReleaseTargetType type, string key, string? note,
                                           CancellationToken ct = default)
    {
        var membri = await BersagliUnitiAsync(type, key, ct).ConfigureAwait(false);
        if (membri.Count == 0) { await PublishNowAsync(type, key, note, ct).ConfigureAwait(false); return; }

        foreach (var m in membri)
        {
            await EnsureCanEditAsync(m.Type, m.Key, ct).ConfigureAwait(false);
            await EnsureNotLockedByOthersAsync(m.Type, m.Key, ct).ConfigureAwait(false);
        }

        // ⚠️ UN solo `now` per tutti: chiederlo dentro il ciclo darebbe ai membri date efficaci diverse di
        // qualche millisecondo, e la selezione della release effettiva ordina proprio per quella.
        var now = DateTime.UtcNow;
        var cycle = _airac.GetCycle(now);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            foreach (var m in membri)
            {
                await SnapshotAndSaveAsync(m.Type, m.Key, cycle, now, note, token).ConfigureAwait(false);
                await _repo.PublishWorkingVersionAsync(m.Type, m.Key, _authz.CurrentUserId ?? 0, cycle, token)
                           .ConfigureAwait(false);
                await PruneArchivedVersionsForTargetAsync(m.Type, m.Key, token).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);

        // Pubblicato -> i documenti restano liberi. ReleaseLockAsync e' no-op se il lock non e' del chiamante.
        foreach (var m in membri)
            await _editing.ReleaseLockAsync(m.DocumentId, _authz.CurrentUserId ?? 0, ct).ConfigureAwait(false);
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
            ?? throw new Aor.ValidationException(Lingua("Release inesistente.", "The release does not exist."));
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);

        var daAnnullare = await SorelleDelloStessoCicloAsync(rel, ct).ConfigureAwait(false);
        if (daAnnullare.Count == 1) { await _repo.CancelAsync(releaseId, ct); return; }

        // Il permesso su OGNI bersaglio prima di toccare qualunque riga, e fuori dalla transazione.
        foreach (var s in daAnnullare)
            await EnsureCanEditAsync(s.Type, s.Key, ct).ConfigureAwait(false);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            foreach (var s in daAnnullare)
                await _repo.CancelAsync(s.Id, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Le release che vanno annullate <b>insieme</b> a questa: lei sola se il documento non e' unito,
    /// altrimenti anche la controparte di ogni altro membro <b>allo stesso ciclo AIRAC</b>.
    ///
    /// <para>⚠️ Di ogni membro si prende la release <b>piu' recente</b> di quel ciclo
    /// (<c>VersionNumber</c> piu' alto), non tutte: e' quella la controparte della release che si sta
    /// annullando — la stessa regola con cui <c>RecomputeStatuses</c> sceglie chi vince per ciclo. Portarsi
    /// via anche le superate cancellerebbe storia che nessuno ha chiesto di cancellare.</para>
    ///
    /// <para>⚠️ Un membro che a quel ciclo non ha pubblicato non ha niente da annullare, e non e' un
    /// errore: puo' essere entrato nell'unione dopo.</para>
    /// </summary>
    private async Task<IReadOnlyList<(ReleaseTargetType Type, string Key, int Id)>> SorelleDelloStessoCicloAsync(
        Vipi.Domain.Entities.DocRelease rel, CancellationToken ct)
    {
        var sola = new[] { (rel.TargetType, rel.TargetKey, rel.Id) };
        var membri = await BersagliUnitiAsync(rel.TargetType, rel.TargetKey, ct).ConfigureAwait(false);
        if (membri.Count == 0) return sola;

        var elenco = new List<(ReleaseTargetType, string, int)>();
        foreach (var m in membri)
        {
            if (m.Type == rel.TargetType && string.Equals(m.Key, rel.TargetKey, StringComparison.OrdinalIgnoreCase))
            {
                elenco.Add((rel.TargetType, rel.TargetKey, rel.Id));
                continue;
            }
            var sue = await _repo.ListAsync(m.Type, m.Key, ct).ConfigureAwait(false);
            var sorella = sue.Where(r => r.ReleaseAiracCycle == rel.ReleaseAiracCycle)
                             .OrderByDescending(r => r.VersionNumber)
                             .FirstOrDefault();
            if (sorella is not null) elenco.Add((m.Type, m.Key, sorella.Id));
        }
        return elenco.Count == 0 ? sola : elenco;
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

    public AiracCycleInfo NextCycle() => _airac.NextCycles(DateTime.UtcNow, 2)[1];

    public async Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(
        ReleaseTargetType type, string key, string? alCiclo = null, CancellationToken ct = default)
    {
        var effettiva = await _repo.GetEffectiveAsync(type, key, DateTime.UtcNow, ct);
        if (effettiva is null) return Array.Empty<ReleaseDiffRow>();

        // Lo snapshot che si otterrebbe pubblicando a quel ciclo. Stesso identico percorso della pubblicazione
        // vera (§3d): se divergessero, la deriva segnalerebbe differenze che al momento di pubblicare non
        // esistono — o, peggio, tacerebbe su quelle che esistono. ⚠️ Vale anche per il ciclo ENTRANTE: è
        // proprio il fatto che BuildSnapshotJsonAsync apra `ShapeReleaseContext` sul ciclo che le si passa a
        // rendere quella domanda sensata, ed è la stessa porta che serve l'anteprima di release.
        var oggiJson = await BuildSnapshotJsonAsync(type, key, alCiclo ?? _airac.GetCycle(DateTime.UtcNow), ct);
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
            ?? throw new Aor.ValidationException(Lingua("Bersaglio della release inesistente.", "The release target does not exist."));
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
            ?? throw new Aor.ValidationException(Lingua(
                "Nessun contenuto da pubblicare: crea prima il documento (bozza).",
                "There is nothing to publish: create the document first (as a draft)."));
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
        // ⚠️ La prosa generata si congela nella lingua SORGENTE del documento, non in quella di chi sta
        // pubblicando: uno snapshot deve dire da che lingua si parte, e chi legge in un'altra la ricompone
        // live. Senza questa forzatura il congelato prenderebbe la cultura del circuito di chi ha premuto
        // Pubblica -- cioe' la stessa release direbbe cose diverse a seconda di chi l'ha fatta.
        var linguaSorgente = payload.Doc.Language == Vipi.Domain.Language.En ? "en" : "it";

        IReadOnlyDictionary<int, string> frozen;
        using (_shapeCycle?.Capturing(cycle))
        using (_linguaProsa?.Rendering(linguaSorgente))
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
        // ⚠️ Documento a lingua BLOCCATA: non c'è niente da congelare, perché non c'è niente da tradurre.
        // Congelare lo stesso non sarebbe innocuo: lo snapshot porterebbe delle rese che il viewer non
        // mostrerà mai, e chi lo aprisse fra un anno leggerebbe una fotografia che dichiara il contrario di
        // quel che quella release faceva vedere (carta 2026-08-31-lingua-bloccata.md §6).
        if (raw.LanguageLocked) return raw;

        var da = sorgente == Vipi.Domain.Language.En ? "en" : "it";
        var segmenti = SegmentiDi(raw).Distinct(StringComparer.Ordinal).ToList();
        if (segmenti.Count == 0) return raw;

        var impronte = segmenti.Select(Translation.TranslationText.Hash).Distinct().ToList();
        var congelate = new Dictionary<string, Dictionary<string, FrozenTranslation>>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in _traduzione.Targets)
        {
            if (string.Equals(a, da, StringComparison.OrdinalIgnoreCase)) continue;
            var note = await _memoriaTraduzioni.LookupAsync(da, a, impronte, ct).ConfigureAwait(false);
            if (note.Count == 0) continue;
            // ⚠️ Si fotografa anche CHI l'ha scritta, non solo che cosa dice. Con la sola stringa il viewer
            // non poteva che dichiarare tutto «non revisionato», e l'avviso su un documento pubblicato non
            // si spegneva piu' — nemmeno con ogni frase corretta a mano. Vedi FrozenTranslation.
            congelate[a] = note.ToDictionary(
                kv => kv.Key,
                kv => new FrozenTranslation(kv.Value.TargetText, kv.Value.Reviewed),
                StringComparer.Ordinal);
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
        // ⚠️ IL TITOLO DEL DOCUMENTO NON C'È, ed è una regola del committente (regole-lingua R4): «vIPI —
        // LIBC Crotone» è il NOME di quel documento, quello che sta nell'elenco, nella briciola di pane e
        // in bocca a chi lo cita in frequenza. Fino al 28 agosto 2026 finiva qui dentro: innocuo — nessuno
        // lo traduce, quindi la memoria non aveva niente da congelare — ma era l'unico posto del prodotto
        // che chiedeva la traduzione di un titolo, e la prossima persona che ne avesse dedotto la regola
        // avrebbe dedotto quella sbagliata. `DocumentTranslator` e `EfTranslatableCorpus` lo escludono
        // entrambi, e ora anche il congelamento.

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
            ?? throw new Aor.ValidationException(Lingua("Bersaglio della release inesistente.", "The release target does not exist."));
        _authz.EnsureAtLeast(VipiRole.Editor);
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
            throw new Aor.ValidationException(Lingua(
                $"Documento in modifica da VID {lk.ByUserId} ({lk.ByName}) fino alle {lk.ExpiresUtc:HH:mm} UTC: la release fotografa la sua bozza, riprova quando ha finito.",
                $"Document being edited by VID {lk.ByUserId} ({lk.ByName}) until {lk.ExpiresUtc:HH:mm} UTC: the release photographs their draft, try again when they are done."));
        return id;
    }
}
