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
    /// Rifiuta se il documento è lockato da un altro editor: lo snapshot fotografa la sua bozza in lavorazione.
    ///
    /// <para>⚠️ <b>Su un documento UNITO pubblica TUTTI i membri</b>, allo stesso ciclo e in una transazione
    /// sola (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c> §6). Non è un di più opzionale: è il
    /// comportamento di questa porta, esattamente come <see cref="CancelReleaseAsync"/> annulla già le sorelle.</para>
    ///
    /// <para>⚠️ <b>Perché non c'è una porta separata.</b> C'era — <c>PublishUnionAsync</c> — ed è durata
    /// mezza giornata: l'elenco di governo continuava a chiamare <i>questa</i>, mostrava la pastiglia
    /// «uniti: 2» e ne pubblicava <b>uno</b>. Due porte per lo stesso gesto, di cui una sola sicura, sono un
    /// invito a chiamare quella sbagliata — e chi la chiama non vede niente di storto, perché il documento
    /// che aveva in mano esce pubblicato davvero. La sicurezza sta nella porta, non nella memoria di chi passa.</para></summary>
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

    /// <summary>Forza la pubblicazione immediata (review): ciclo corrente, effettiva adesso. Rifiuta se il documento
    /// è lockato da un altro editor (promuoverebbe la sua bozza a metà); a pubblicazione avvenuta rilascia
    /// l'eventuale lock del chiamante, come il publish-versione dell'editor.
    /// <para><inheritdoc cref="PublishAsync" path="/summary/para[1]"/> ⚠️ E promuove la bozza di <b>ogni</b>
    /// membro: le due semantiche restano diverse anche unite — la pianificata non promuove, questa sì.</para>
    /// <para>U-241 (revisione 3; scelta del committente del 28-set-2026): un bersaglio il cui contenuto è identico,
    /// byte per byte, alla release in vigore — e che non ha programmate future da scavalcare — non riceve una
    /// release nuova. La bozza si promuove lo stesso.</para></summary>
    /// <returns><c>false</c> se nessun bersaglio aveva modifiche: non è stata creata nessuna release.</returns>
    Task<bool> PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default);

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

    /// <summary>
    /// Il ciclo di una release <b>programmata</b> che porta già lo stato di lavorazione di oggi, o
    /// <c>null</c> se non ce n'è nessuna. Operazione di sistema: nessuna autorizzazione.
    ///
    /// <para><b>Perché serve.</b> <see cref="DriftFromEffectiveAsync"/> confronta con la release
    /// <b>in vigore</b>, e una programmata non lo è per definizione: chi programma al ciclo entrante — che è
    /// il gesto giusto — si vede rispondere «la copia pubblicata è indietro» e resta a vederselo chiedere
    /// fino al rollover, cioè per settimane, senza nessun modo di farlo tacere. Ma l'azione che chiude
    /// quella riga <b>è stata fatta</b>: il pubblico vedrà il nuovo al ciclo, e la timeline lo dice già.</para>
    ///
    /// <para>⚠️ Il confronto è la stessa firma editoriale della deriva, e lo snapshot si chiede <b>al ciclo
    /// di quella release</b>: le derivate che dipendono dal ciclo risponderebbero altro, e si direbbe
    /// «diversa» una programmata identica.</para>
    ///
    /// <para>⚠️ Se la bozza cambia <i>dopo</i> aver programmato, le firme tornano a divergere e la riga
    /// riappare — che è giusto: quella programmata porta un testo che non è più quello che si vuole. Lo stesso se
    /// cambiano le <b>derivate congelate</b> (una TORA, un minimo LVP): dal 28 settembre 2026 (U-053) il
    /// confronto guarda anche quelle, che nessun conteggio di blocchi vede.</para>
    /// </summary>
    Task<string?> ProgrammataAllineataAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>Il ciclo AIRAC <b>entrante</b> con la sua data efficace: il primo che non è ancora in vigore.</summary>
    AiracCycleInfo NextCycle();

    /// <summary>Sweep di retention su tutti i documenti gestiti (system op, nessuna authz):
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
        ReadingLanguageContext? linguaProsa = null,
        Lazy<IImpactDriftUseCase>? deriva = null,
        IImportStateStore? stati = null,
        IDocLinkService? collegamenti = null,
        Lazy<IRemotizzazioneService>? spostamenti = null)
    {
        _spostamenti = spostamenti;
        _collegamenti = collegamenti;
        _stati = stati;
        _deriva = deriva;
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

    /// <summary>
    /// Il rivelatore della deriva, per rivalutare i bersagli <b>appena</b> pubblicati.
    ///
    /// <para>⚠️ <c>Lazy</c> e non iniezione diretta perché <c>ImpactDriftUseCase</c> dipende a sua volta da
    /// <see cref="IReleaseService"/>: chiederli l'uno nel costruttore dell'altro è un ciclo, e il contenitore
    /// lo rifiuta alla prima risoluzione. Il pigro rompe il ciclo nel punto giusto — la deriva serve
    /// <b>dopo</b> che la release è scritta, mai per costruire questo servizio.</para>
    ///
    /// <para>Opzionale: senza, la riconciliazione resta al solo giro notturno, che è il comportamento di
    /// prima del 7 settembre 2026.</para>
    /// </summary>
    private readonly Lazy<IImpactDriftUseCase>? _deriva;

    /// <summary>Dove resta scritto se la riconciliazione alla pubblicazione riesce. Opzionale: senza, il
    /// guasto torna a essere invisibile — vedi <see cref="AnnotaEsitoAsync"/>.</summary>
    private readonly IImportStateStore? _stati;

    /// <summary>I documenti collegati (§A109), congelati nella release SOLO quando la si scrive: la deriva e le
    /// anteprime rifanno lo snapshot a ogni apertura d'editor, e la firma del diff non li guarda. Null = i banchi
    /// che costruiscono il servizio a mano: la release esce senza il campo, e la pagina li calcola dal vivo.</summary>
    private readonly IDocLinkService? _collegamenti;

    /// <summary>Gli spostamenti degli enti nella vIPI dell'ACC (S52): si concludono quando la vIPI ACC col gruppo va
    /// in vigore. Null = i banchi che costruiscono il servizio a mano; la rete è il giro delle release.</summary>
    private readonly Lazy<IRemotizzazioneService>? _spostamenti;

    /// <summary>
    /// Dopo una pubblicazione della vIPI ACC: gli enti copiati nel suo gruppo APP passano alla vIPI ACC, e la loro
    /// vIPI APP si nasconde (revisione degli enti ATC, S52). ⚠️ Fuori dalla transazione e a prova di guasto, come
    /// la deriva: la pubblicazione è già riuscita, e se questo salta lo rifà il giro delle release.
    /// </summary>
    private async Task ConcludiSpostamentiAsync(IEnumerable<ReleaseTargetType> tipi, CancellationToken ct)
    {
        if (_spostamenti is null || !tipi.Contains(ReleaseTargetType.AccVipi)) return;
        try
        {
            await _spostamenti.Value.ConcludiSpostamentiAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { /* la rete è il giro delle release (ReleaseSweepHostedService). */ }
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

    public async Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note,
                                   CancellationToken ct = default)
    {
        var membri = await BersagliUnitiAsync(type, key, ct).ConfigureAwait(false);
        if (membri.Count == 0)
        {
            await EnsureCanEditAsync(type, key, ct);
            var solo = await EnsureNotLockedByOthersAsync(type, key, ct);

            // ⚠️ Nella STESSA transazione del ramo dell'unione, dodici righe più sotto. Fino al 7 settembre
            // 2026 questa riga stava fuori, e la differenza non aveva una ragione: un documento solo è il
            // caso N=1 dell'altro, non un'operazione diversa. `SnapshotAndSaveAsync` scrive più di una
            // volta — la fotografia e la riga di release, con `VersionNumber` = max+1 letto in memoria
            // sotto un indice UNICO — quindi senza rete un secondo salvataggio che collide lascia dietro
            // metà lavoro (revisione del 6 settembre 2026, R-017).
            await _uow.ExecuteInTransactionAsync(
                token => SnapshotAndSaveAsync(
                    type, key, releaseCycle, _airac.EffectiveUtcForCycle(releaseCycle), note, token),
                ct).ConfigureAwait(false);
            // ⚠️ Anche — anzi SOPRATTUTTO — sulla programmata: una release a ciclo futuro non diventa quella
            // in vigore, quindi la deriva continuerebbe a confrontare con la vecchia e a chiedere di
            // ripubblicare per settimane a chi ha appena fatto il gesto giusto.
            await RiconciliaDerivaAsync(new[] { solo ?? 0 }, ct).ConfigureAwait(false);
            await ConcludiSpostamentiAsync(new[] { type }, ct).ConfigureAwait(false);
            return;
        }

        // ⚠️ I cancelli PRIMA, TUTTI, e fuori dalla transazione: un permesso negato o un lock altrui non sono
        // scritture da annullare, e scoprirli a metà elenco vorrebbe dire aver già fotografato qualcuno.
        foreach (var m in membri)
        {
            await EnsureCanEditAsync(m.Type, m.Key, ct).ConfigureAwait(false);
            await EnsureNotLockedByOthersAsync(m.Type, m.Key, ct).ConfigureAwait(false);
        }

        // ⚠️ Tutto o niente, in UNA transazione. `SaveReleaseAsync` fa un SaveChanges per chiamata e
        // `VersionNumber` è max+1 letto in memoria sotto un indice UNICO: due salvataggi in fila non sono
        // atomici, e un secondo membro che collide lascerebbe il primo pubblicato da solo — metà unione a un
        // ciclo e metà a un altro, cioè la desincronizzazione che l'accoppiamento esiste per togliere.
        // ⚠️ La data efficace si calcola UNA VOLTA dal ciclo: passandola a tutti, i membri escono con la
        // stessa senza copiarla a mano. È questo che fa funzionare anche la pianificata.
        var effectiveUtc = _airac.EffectiveUtcForCycle(releaseCycle);
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            // ⚠️ IN SEQUENZA, mai in parallelo: la cattura apre `ShapeReleaseContext.Capturing`, che NON è
            // annidabile (il suo Dispose azzera), e `ReadingLanguageContext.Rendering` con la lingua sorgente
            // di QUEL membro. Due catture sovrapposte congelerebbero l'una nel contesto dell'altra.
            foreach (var m in membri)
                await SnapshotAndSaveAsync(m.Type, m.Key, releaseCycle, effectiveUtc, note, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        await RiconciliaDerivaAsync(membri.Select(m => m.DocumentId), ct).ConfigureAwait(false);
        await ConcludiSpostamentiAsync(membri.Select(m => m.Type), ct).ConfigureAwait(false);
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
    public async Task<bool> PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default)
    {
        // I bersagli: i membri dell'unione, o questo documento solo. ⚠️ `DocumentId` serve dopo, per mollare
        // il lock: sui membri arriva dai descrittori, sul singolo dal controllo del lock.
        var membri = await BersagliUnitiAsync(type, key, ct).ConfigureAwait(false);
        (ReleaseTargetType Type, string Key, int DocumentId)[] bersagli;
        if (membri.Count == 0)
        {
            await EnsureCanEditAsync(type, key, ct);
            var docId = await EnsureNotLockedByOthersAsync(type, key, ct);
            bersagli = new[] { (type, key, docId ?? 0) };
        }
        else
        {
            foreach (var m in membri)
            {
                await EnsureCanEditAsync(m.Type, m.Key, ct).ConfigureAwait(false);
                await EnsureNotLockedByOthersAsync(m.Type, m.Key, ct).ConfigureAwait(false);
            }
            bersagli = membri.Select(m => (m.Type, m.Key, m.DocumentId)).ToArray();
        }

        // ⚠️ UN solo `now` per tutti: chiederlo dentro il ciclo darebbe ai bersagli date efficaci diverse di
        // qualche millisecondo, e la selezione della release effettiva ordina proprio per quella.
        var now = DateTime.UtcNow;
        var cycle = _airac.GetCycle(now);

        var create = 0;
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            create = 0;   // la strategia di retry può rifare il blocco: si conta da capo
            foreach (var t in bersagli)
            {
                if (await SnapshotAndSaveAsync(t.Type, t.Key, cycle, now, note, token, saltaSeIdentica: true)) create++;
                // Pubblicazione IMMEDIATA (review): promuove anche la bozza a versione pubblicata, così lo stato del
                // documento e quello della release restano allineati (la pill dell'editor, la storia versioni, il diff).
                // La VISIBILITÀ pubblica non dipende più da questo: dal doc 10 §S6b è la release effettiva a decidere, e
                // il fallback live del viewer non c'è più — questo commento diceva ancora il contrario.
                // Le release SCHEDULATE (PublishAsync a ciclo futuro) NON promuovono: restano solo snapshot per il ciclo.
                await _repo.PublishWorkingVersionAsync(t.Type, t.Key, _authz.CurrentUserId ?? 0, cycle, token);
                // Retention versioni: DOPO la promozione (che archivia la precedente), così il conteggio Archived include la
                // versione appena archiviata → cap esatto, non N+1. Lo scheduled non promuove/archivia → non serve qui.
                await PruneArchivedVersionsForTargetAsync(t.Type, t.Key, token);
            }
        }, ct);

        // Pubblicato → i documenti restano liberi, come dopo il publish-versione dell'editor
        // (EditingService.PublishAsync). ReleaseLockAsync è no-op se il lock non è del chiamante.
        foreach (var t in bersagli)
            if (t.DocumentId > 0)
                await _editing.ReleaseLockAsync(t.DocumentId, _authz.CurrentUserId ?? 0, ct);

        await RiconciliaDerivaAsync(bersagli.Select(t => t.DocumentId), ct).ConfigureAwait(false);
        await ConcludiSpostamentiAsync(bersagli.Select(t => t.Type), ct).ConfigureAwait(false);
        return create > 0;
    }

    /// <summary>
    /// Rivaluta <b>subito</b> la deriva dei documenti appena pubblicati (o appena rimasti senza una
    /// release), così la lista «Da fare» dice il vero appena si esce dal pannello.
    ///
    /// <para><b>Che cosa riparava.</b> Fino al 7 settembre 2026 la riga «la copia pubblicata è indietro» la
    /// chiudeva soltanto il giro delle 24 ore: chi pubblicava continuava a vedersi chiedere il lavoro che
    /// aveva appena fatto, per un giorno intero, e non aveva <b>nessun</b> modo di sapere se la
    /// pubblicazione fosse andata a segno. Misurato dal vivo su LIBD: riga aperta il 6 alle 19:27Z,
    /// ripubblicato il 7 alle 06:52, riga ancora lì a metà mattina.</para>
    ///
    /// <para>⚠️ <b>Fuori dalla transazione, e a prova di guasto.</b> La release è già scritta e promossa:
    /// un errore qui non deve far dire «pubblicazione fallita» a una pubblicazione riuscita, né annullarla.
    /// Se salta, resta la rete del giro notturno — cioè si torna esattamente al comportamento di prima, che
    /// è il peggio che possa succedere e non è una rottura.</para>
    ///
    /// <para>⚠️ L'annullamento della richiesta invece <b>passa</b>: quello non è un guasto, è qualcuno che
    /// ha chiuso la pagina, e inghiottirlo vorrebbe dire continuare a lavorare per nessuno.</para>
    /// </summary>
    private async Task RiconciliaDerivaAsync(IEnumerable<int> documentIds, CancellationToken ct)
    {
        if (_deriva is null) return;

        // Il primo guasto, non l'ultimo: è quello che è successo per primo a spiegare gli altri, e i
        // successivi su un'unione sono di solito la stessa causa vista N volte.
        Exception? guasto = null;

        foreach (var id in documentIds.Where(x => x > 0).Distinct())
        {
            try
            {
                await _deriva.Value.RunForDocumentAsync(id, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { guasto ??= ex; /* vedi sopra: la rete è il giro notturno. */ }
        }

        await AnnotaEsitoAsync(guasto, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Lascia scritto <b>com'è andata</b> la riconciliazione di cui sopra, sulla chiave
    /// <see cref="ImportCategories.ImpactDriftOnPublish"/>: la legge Diagnostica.
    ///
    /// <para>⚠️ <b>È il pezzo che mancava.</b> Ingoiare il guasto è giusto — la release è scritta, e una
    /// pubblicazione riuscita non deve dirsi fallita — ma ingoiarlo <b>in silenzio</b> rendeva il sintomo
    /// indistinguibile dal difetto di partenza: chi ripubblicava vedeva l'avviso restare, e non c'era una
    /// riga da nessuna parte a dire che il ricalcolo non era mai avvenuto. <c>Vipi.Application</c> non ha un
    /// logger, e questa tabella è la porta che c'è già.</para>
    ///
    /// <para>Il successo si scrive <b>sempre</b>, non solo il guasto: <c>MarkSuccessAsync</c> azzera
    /// l'errore precedente, quindi la riga dice «l'ultimo guasto è ancora vero» e non «una volta, chissà
    /// quando, andò male». È anche la risposta a «ha mai funzionato?».</para>
    ///
    /// <para>⚠️ A prova di guasto <b>a sua volta</b>, e per la stessa ragione della riconciliazione: se
    /// scrivere l'annotazione esplodesse, una pubblicazione riuscita si direbbe fallita per colpa di una
    /// nota di diagnostica.</para>
    /// </summary>
    private async Task AnnotaEsitoAsync(Exception? guasto, CancellationToken ct)
    {
        if (_stati is null) return;

        try
        {
            if (guasto is null)
                await _stati.MarkSuccessAsync(ImportCategories.ImpactDriftOnPublish, DateTime.UtcNow, ct)
                    .ConfigureAwait(false);
            else
                await _stati.MarkFailureAsync(ImportCategories.ImpactDriftOnPublish, DateTime.UtcNow,
                        $"{guasto.GetType().Name}: {guasto.Message}", ct)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { /* una nota che non si scrive non è una pubblicazione fallita. */ }
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
    // ⚠️ Qui c'era `BackfillMissingReleasesAsync`, la «migrazione A» di luglio (doc 10 §3f): a OGNI avvio
    // pubblicava, firmando «sistema», la versione di lavoro — BOZZA compresa — di ogni documento Published
    // senza release in vigore. Finita la migrazione, quell'ingresso lo aprivano solo casi in cui pubblicare
    // è sbagliato: l'Editor che annulla l'unica release (riprodotto: LIRA tornata pubblica con la bozza dopo
    // un riavvio), il documento con la sola programmata, lo scheletro di vLOA generato (la 65 dell'8-set).
    // Tolto il 27-set-2026 (U-006, revisione totale 3): una release la crea solo il gesto di un Editor.

    public async Task CancelReleaseAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct)
            ?? throw new Aor.ValidationException(Lingua("Release inesistente.", "The release does not exist."));
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);

        var daAnnullare = await SorelleDelloStessoCicloAsync(rel, ct).ConfigureAwait(false);
        if (daAnnullare.Count == 1)
        {
            await _repo.CancelAsync(releaseId, ct);
            // ⚠️ Anche qui, e per il verso opposto: annullare una release può rendere VERA una deriva che
            // non c'era: la copia in vigore torna a essere la precedente. Senza questo, la lista tace su un
            // documento che è appena tornato indietro — e tace fino a domani.
            await RiconciliaDerivaAsync(await DocumentiDeiBersagliAsync(daAnnullare, ct), ct).ConfigureAwait(false);
            return;
        }

        // Il permesso su OGNI bersaglio prima di toccare qualunque riga, e fuori dalla transazione.
        foreach (var s in daAnnullare)
            await EnsureCanEditAsync(s.Type, s.Key, ct).ConfigureAwait(false);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            foreach (var s in daAnnullare)
                await _repo.CancelAsync(s.Id, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        await RiconciliaDerivaAsync(await DocumentiDeiBersagliAsync(daAnnullare, ct), ct).ConfigureAwait(false);
    }

    /// <summary>Gli Id documento dei bersagli indicati, per la riconciliazione. Un bersaglio che non risolve
    /// più a un documento — è appena stato eliminato — semplicemente non torna.</summary>
    private async Task<IReadOnlyList<int>> DocumentiDeiBersagliAsync(
        IReadOnlyList<(ReleaseTargetType Type, string Key, int Id)> bersagli, CancellationToken ct)
    {
        var ids = new List<int>();
        foreach (var b in bersagli)
            if (await _targets.For(b.Type).ResolveDocumentIdAsync(b.Key, ct).ConfigureAwait(false) is int docId)
                ids.Add(docId);
        return ids;
    }

    /// <summary>
    /// Le release che vanno annullate <b>insieme</b> a questa: lei sola se il documento non e' unito,
    /// altrimenti anche la controparte di ogni altro membro <b>allo stesso ciclo AIRAC</b>.
    ///
    /// <para>⚠️ Di ogni membro si prende <b>una</b> release di quel ciclo, quella allo stesso posto (contando
    /// dalla piu' recente) della release che si sta annullando — non tutte. Portarsi via anche le altre
    /// cancellerebbe storia che nessuno ha chiesto di cancellare.</para>
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

        // 🔴 T-008 (revisione del 13 settembre 2026): la sorella è quella della STESSA pubblicazione, cioè allo
        // stesso POSTO nel ciclo contando dalla più recente — non sempre la più recente. Prima, annullare una
        // release superata del militare annullava quella IN VIGORE del civile. Se i membri non hanno lo stesso
        // numero di release nel ciclo il posto non dice niente: la più recente resta accoppiata alla più recente
        // (è la pubblicazione congiunta in vigore), una superata si annulla da sola.
        var mie = (await _repo.ListAsync(rel.TargetType, rel.TargetKey, ct).ConfigureAwait(false))
            .Where(r => r.ReleaseAiracCycle == rel.ReleaseAiracCycle)
            .OrderByDescending(r => r.VersionNumber)
            .ToList();
        var posto = mie.FindIndex(r => r.Id == rel.Id);
        if (posto < 0) return sola;

        var elenco = new List<(ReleaseTargetType, string, int)>();
        foreach (var m in membri)
        {
            if (m.Type == rel.TargetType && string.Equals(m.Key, rel.TargetKey, StringComparison.OrdinalIgnoreCase))
            {
                elenco.Add((rel.TargetType, rel.TargetKey, rel.Id));
                continue;
            }
            var sue = (await _repo.ListAsync(m.Type, m.Key, ct).ConfigureAwait(false))
                .Where(r => r.ReleaseAiracCycle == rel.ReleaseAiracCycle)
                .OrderByDescending(r => r.VersionNumber)
                .ToList();
            if (sue.Count == 0) continue;
            if (sue.Count == mie.Count) elenco.Add((m.Type, m.Key, sue[posto].Id));
            else if (posto == 0) elenco.Add((m.Type, m.Key, sue[0].Id));
        }
        return elenco.Count <= 1 ? sola : elenco;
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
        var prev = baseline is null ? new Dictionary<string, Voce>(StringComparer.OrdinalIgnoreCase)
                                    : Signature(baseline.PayloadJson);
        var rows = Confronta(cur, prev);

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

        return Confronta(Signature(oggiJson), Signature(effettiva.PayloadJson));
    }

    public async Task<string?> ProgrammataAllineataAsync(ReleaseTargetType type, string key,
        CancellationToken ct = default)
    {
        // ⚠️ «Programmata» si misura sulla DATA, non sulla colonna `Status`: quella la riscrive
        // `RecomputeStatuses`, che gira al salvataggio e all'annullo — fra un gesto e l'altro una riga
        // invecchia da sola ed entra in vigore senza che nessuno l'abbia toccata.
        var now = DateTime.UtcNow;
        var future = (await _repo.ListAsync(type, key, ct))
            .Where(r => r.ReleaseEffectiveUtc > now && r.Status != ReleaseStatus.Superseded)
            .OrderBy(r => r.ReleaseEffectiveUtc).ThenByDescending(r => r.VersionNumber)
            .ToList();
        if (future.Count == 0) return null;

        foreach (var r in future)
        {
            ct.ThrowIfCancellationRequested();
            var rel = await _repo.GetByIdAsync(r.Id, ct);
            if (rel is null) continue;

            var oggiJson = await BuildSnapshotJsonAsync(type, key, rel.ReleaseAiracCycle, ct);
            if (oggiJson is null) continue;

            // 🔴 U-053 (revisione totale 3): e le DERIVATE congelate. La firma editoriale conta i blocchi, e una
            // TORA o un minimo LVP corretti dopo aver programmato non cambiano nessun conteggio: la programmata
            // copriva ancora la riga «da ripubblicare» mentre portava i valori vecchi.
            if (StesseFirme(Signature(oggiJson), Signature(rel.PayloadJson))
                && StesseCongelate(FirmaCongelate(oggiJson), FirmaCongelate(rel.PayloadJson)))
                return rel.ReleaseAiracCycle;
        }
        return null;
    }

    /// <summary>Due firme editoriali dicono la stessa cosa? Il confronto non trova nessuna differenza.</summary>
    private static bool StesseFirme(Dictionary<string, Voce> a, Dictionary<string, Voce> b) =>
        Confronta(a, b).Count == 0;

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

    /// <summary>Una voce della firma: l'etichetta che si mostra (il percorso dei TITOLI) e il numero di blocchi.</summary>
    private sealed record Voce(string Etichetta, int Blocchi);

    /// <summary>Firma editoriale di un payload: identità della sezione → voce. Base del diff e della deriva.
    /// Post-08 tutti i tipi sono su DocReleasePayload → firma unica, nessuno switch per-tipo.
    /// <para>⚠️ L'identità di una sezione di CATALOGO è la sua chiave, non il titolo (revisione 3, U-249). Il titolo
    /// di una sezione di catalogo non lo sceglie nessuno — lo risolve <c>TitoliDiCatalogo</c> a view-time, e il DB
    /// lo tiene nella lingua di nascita — quindi una sua riscrittura (la riconciliazione dei titoli d'aeroporto,
    /// una rinomina del catalogo) non è una modifica del documento. Col percorso dei titoli contava come una sezione
    /// tolta più una aggiunta, e apriva una riga «da ripubblicare» che nessun lettore poteva vedere: LIRL, 21-set,
    /// «Airport charts, Airport charts / Aerodromo…».</para>
    /// <para>Per titolo restano le sezioni LIBERE (<c>custom:…</c>), e le chiavi ripetute fra sorelle
    /// (<c>appgroup</c> nella vIPI ACC, lo storico <c>custom</c> nudo): lì la chiave non dice quale sezione è.</para>
    /// </summary>
    /// <summary>
    /// Le derivate congelate dello snapshot, per identità di sezione — la stessa della firma editoriale: chiave di
    /// catalogo, o percorso di titoli. ⚠️ Non per Id: gli Id sono quelli della versione di lavoro al momento della
    /// cattura, e una «Pubblica versione» fra la programmazione e oggi li cambia senza che cambi niente.
    /// </summary>
    private static Dictionary<string, string> FirmaCongelate(string payloadJson)
    {
        var firma = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var p = JsonSerializer.Deserialize<DocReleasePayload>(payloadJson);
            if (p?.Doc?.Roots is not null && p.FrozenSections is { Count: > 0 } congelate)
                FlattenSections(p.Doc.Roots, "", "", new Dictionary<string, Voce>(StringComparer.OrdinalIgnoreCase),
                    (id, s) => { if (congelate.TryGetValue(s.Id, out var json)) firma[id] = json; });
        }
        catch (JsonException) { }
        return firma;
    }

    private static bool StesseCongelate(Dictionary<string, string> a, Dictionary<string, string> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && string.Equals(v, kv.Value, StringComparison.Ordinal));

    private static Dictionary<string, Voce> Signature(string payloadJson)
    {
        var sig = new Dictionary<string, Voce>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var p = JsonSerializer.Deserialize<DocReleasePayload>(payloadJson);
            if (p?.Doc?.Roots is not null) FlattenSections(p.Doc.Roots, "", "", sig);
        }
        catch (JsonException) { }
        return sig;
    }

    private static void FlattenSections(IReadOnlyList<RawSection> sections, string idPrefix, string labelPrefix,
        Dictionary<string, Voce> sig, Action<string, RawSection>? ogni = null)
    {
        var ripetute = sections.GroupBy(s => s.SectionKey ?? "", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sections)
        {
            var perChiave = !string.IsNullOrEmpty(s.SectionKey) && !SectionKeys.IsCustom(s.SectionKey)
                            && !ripetute.Contains(s.SectionKey);
            // «#» davanti: una chiave non si confonde mai con un titolo che le somigli.
            var id = perChiave ? "#" + s.SectionKey : s.Title;
            var idPath = idPrefix.Length == 0 ? id : $"{idPrefix} / {id}";
            var label = labelPrefix.Length == 0 ? s.Title : $"{labelPrefix} / {s.Title}";
            sig[idPath] = new Voce(label, s.Blocks.Count);
            ogni?.Invoke(idPath, s);
            if (s.Children.Count > 0) FlattenSections(s.Children, idPath, label, sig, ogni);
        }
    }

    /// <summary>
    /// Il confronto fra due firme: aggiunte, modificate (blocchi diversi), tolte. Etichetta dalla firma nuova,
    /// dalla vecchia per le tolte.
    /// <para>⚠️ Due passate. La prima per identità. La seconda accoppia fra loro le voci rimaste spaiate che hanno
    /// lo stesso percorso di TITOLI: una release vecchia può avere la stessa sezione con una chiave libera (le
    /// sezioni «cotte» degli aeroporti, prima della riconciliazione delle chiavi) — cambiata la chiave, non è
    /// cambiato il documento. Senza questa rete, il passaggio all'identità per chiave avrebbe aperto righe
    /// «da ripubblicare» su tutti i documenti pubblicati prima di quella riconciliazione.</para>
    /// </summary>
    private static List<ReleaseDiffRow> Confronta(Dictionary<string, Voce> nuova, Dictionary<string, Voce> vecchia)
    {
        var aggiunte = nuova.Where(kv => !vecchia.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
        var tolte = vecchia.Where(kv => !nuova.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
        var righe = new List<ReleaseDiffRow>();

        foreach (var kv in nuova)
            if (vecchia.TryGetValue(kv.Key, out var v) && v.Blocchi != kv.Value.Blocchi)
                righe.Add(new ReleaseDiffRow(kv.Value.Etichetta, ReleaseChangeKind.Modified, v.Blocchi, kv.Value.Blocchi));

        foreach (var a in aggiunte)
        {
            var gemella = tolte.FirstOrDefault(t => string.Equals(t.Etichetta, a.Etichetta, StringComparison.OrdinalIgnoreCase));
            if (gemella is null)
            {
                righe.Add(new ReleaseDiffRow(a.Etichetta, ReleaseChangeKind.Added, null, a.Blocchi));
                continue;
            }
            tolte.Remove(gemella);
            if (gemella.Blocchi != a.Blocchi)
                righe.Add(new ReleaseDiffRow(a.Etichetta, ReleaseChangeKind.Modified, gemella.Blocchi, a.Blocchi));
        }
        foreach (var t in tolte)
            righe.Add(new ReleaseDiffRow(t.Etichetta, ReleaseChangeKind.Removed, t.Blocchi, null));

        // L'ordine di prima: le presenti per etichetta, poi le tolte per etichetta.
        return righe.OrderBy(r => r.Change == ReleaseChangeKind.Removed)
            .ThenBy(r => r.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <returns><c>false</c> se la release non è stata scritta perché identica a quella in vigore (U-241).</returns>
    private async Task<bool> SnapshotAndSaveAsync(ReleaseTargetType type, string key, string cycle, DateTime effectiveUtc,
        string? note, CancellationToken ct, bool saltaSeIdentica = false)
    {
        var finalJson = await BuildSnapshotJsonAsync(type, key, cycle, ct, conCollegamenti: true)
            ?? throw new Aor.ValidationException(Lingua(
                "Nessun contenuto da pubblicare: crea prima il documento (bozza).",
                "There is nothing to publish: create the document first (as a draft)."));
        if (saltaSeIdentica && await IdenticaAllaInVigoreAsync(type, key, effectiveUtc, finalJson, ct)) return false;
        await _repo.SaveReleaseAsync(type, key, cycle, effectiveUtc, finalJson, _authz.CurrentUserId ?? 0, note, ct);
        // Retention per-publish (release Superseded): sia per l'immediato sia per lo schedulato. Le versioni Archived
        // si potano solo dopo la promozione della bozza (PublishNowAsync) → vedi PruneArchivedVersionsForTargetAsync.
        await _repo.PruneReleasesAsync(type, key, KeepSupersededFromUtc(), ct);
        return true;
    }

    /// <summary>
    /// La fotografia di adesso è la release in vigore, byte per byte, e non c'è nessuna programmata futura (U-241).
    /// <para>⚠️ Il confronto è sul payload INTERO e non sulla firma editoriale: quella conta i blocchi, e un testo
    /// corretto dentro un blocco non la cambia. Saltare per firma perderebbe una modifica vera.</para>
    /// <para>⚠️ Con una programmata futura non si salta mai: «Pubblica ora» ha il numero più alto e la scavalca
    /// (EfReleaseRepository.RecomputeStatuses, U-009), ed è spesso proprio per questo che la si preme.</para>
    /// </summary>
    private async Task<bool> IdenticaAllaInVigoreAsync(ReleaseTargetType type, string key, DateTime adesso,
        string payloadJson, CancellationToken ct)
    {
        var elenco = await _repo.ListAsync(type, key, ct);
        if (elenco.Any(r => r.ReleaseEffectiveUtc > adesso && r.Status != ReleaseStatus.Superseded)) return false;
        var inVigore = await _repo.GetEffectiveAsync(type, key, adesso, ct);
        return inVigore is not null && string.Equals(inVigore.PayloadJson, payloadJson, StringComparison.Ordinal);
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
    private async Task<string?> BuildSnapshotJsonAsync(ReleaseTargetType type, string key, string cycle, CancellationToken ct,
        bool conCollegamenti = false)
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

        // La STRUTTURA di oggi, con tutti i candidati: chi di loro si vede lo decide la pagina (§A109, scelta A).
        if (conCollegamenti && _collegamenti is not null)
            payload.Collegamenti = await _collegamenti.CaptureAsync(type, key, ct).ConfigureAwait(false);
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
