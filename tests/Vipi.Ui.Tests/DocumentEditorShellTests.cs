using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il guscio dei quattro editor documentali: quel che succede quando un gesto <b>fallisce</b>.
///
/// <para>
/// ⚠️ <b>Il modo in cui un editor si rompe peggio non è l'errore: è il silenzio.</b> Segnalazione dal campo
/// (1 settembre 2026): «la pagina si blocca in salvataggio e si deve ricaricare per farla salvare». Il badge
/// resta su «Salvataggio…», non compare nessun messaggio, e chi sta scrivendo non sa se ha perso il lavoro.
/// Questi test presidiano le due strade per cui quel silenzio arriva — un'eccezione FUORI dal guardiano, e
/// un guardiano che rimette a posto lo stato ma non chiede il ridisegno.
/// </para>
///
/// <para>Sono test sul guscio e non sull'editor montato: il guscio è una classe apposta perché il
/// comportamento si potesse provare senza una fixture con DbContext, lock e JS.</para>
/// </summary>
public class DocumentEditorShellTests
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>JS che non fa niente: il guscio lo chiama solo per aprire/chiudere le sezioni.</summary>
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => default;
    }

    /// <summary>
    /// Servizio di editing finto: implementa i tre metodi che il guscio usa davvero e rifiuta gli altri.
    /// <para>⚠️ Gli altri ventitré <b>sollevano</b> invece di tornare un valore innocuo: se un giorno il
    /// guscio ne chiamasse uno, deve cadere il test — non passare in silenzio.</para>
    /// </summary>
    private sealed class EditingFinto : IEditingService
    {
        public Func<int, Task>? SulRilascio { get; init; }
        public Func<int, Task<LockInfo>>? SullIspezione { get; init; }
        public Func<int, Task>? SullaBozza { get; init; }

        public int RilasciChiesti { get; private set; }

        public Task ReleaseLockAsync(int documentId, CancellationToken ct = default)
        {
            RilasciChiesti++;
            return SulRilascio?.Invoke(documentId) ?? Task.CompletedTask;
        }

        public Task<LockInfo> InspectLockAsync(int documentId, CancellationToken ct = default) =>
            SullIspezione?.Invoke(documentId) ?? Task.FromResult(LockInfo.Free());

        public Task<int> CreateDraftAsync(int documentId, CancellationToken ct = default)
        {
            SullaBozza?.Invoke(documentId).GetAwaiter().GetResult();
            return Task.FromResult(1);
        }

        public Task<LockInfo> AcquireLockAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult(LockInfo.Free());

        // ---- il resto non lo tocca il guscio ----
        private static Exception NonUsato([System.Runtime.CompilerServices.CallerMemberName] string? m = null) =>
            new NotSupportedException($"Il guscio non deve chiamare {m}.");

        public Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default) => throw NonUsato();
        public Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default) => throw NonUsato();
        public Task<int?> ResolveVloaIdByPairAsync(string homeAccCode, string foreignAccCode, CancellationToken ct = default) => throw NonUsato();
        public Task<int> CreateDocumentAsync(DocumentType type, string title, IReadOnlyList<int>? scopeSectorIds,
            int? primarySectorId, int? homeSectorId, int? neighbourSectorId, CancellationToken ct = default) => throw NonUsato();
        public Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default) => throw NonUsato();
        public Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default) => throw NonUsato();
        public Task DeleteBlockAsync(int blockId, CancellationToken ct = default) => throw NonUsato();
        public Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionRenderModeAsync(int sectionId, RenderMode mode, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionHiddenAsync(int sectionId, bool hidden, CancellationToken ct = default) => throw NonUsato();
        public Task<IReadOnlyList<SezioneComune>> SezioniComuniAsync(IReadOnlyList<(int DocumentId, ReleaseTargetType Famiglia)> membri, CancellationToken ct = default) => throw NonUsato();
        public Task<int> ApplicaSezioniComuniAsync(IReadOnlyList<int> nascondiIn, IReadOnlyList<(int DocumentId, ReleaseTargetType Famiglia)> membri, IReadOnlyList<string> chiavi, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionAudienceAsync(int sectionId, SectionAudience audience, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionBeforeParentBodyAsync(int sectionId, bool before, CancellationToken ct = default) => throw NonUsato();
        public Task SetSectionLeadSentenceAsync(int sectionId, bool lead, CancellationToken ct = default) => throw NonUsato();
        public Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default) => throw NonUsato();
        public Task DeleteSectionAsync(int sectionId, CancellationToken ct = default) => throw NonUsato();
        public Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default) => throw NonUsato();
        public Task MoveSectionBeforeAsync(int sectionId, int? beforeSectionId, CancellationToken ct = default) => throw NonUsato();
        public Task MoveSectionToParentAsync(int sectionId, int? newParentSectionId, int? beforeSectionId, CancellationToken ct = default) => throw NonUsato();
        public Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default) => throw NonUsato();
        public Task PublishAsync(int versionId, string? note, CancellationToken ct = default) => throw NonUsato();
        public Task<int> DiscardDraftAsync(int versionId, CancellationToken ct = default) => throw NonUsato();
        public Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default) => throw NonUsato();
        public Task ForceUnlockAsync(int documentId, CancellationToken ct = default) => throw NonUsato();
    }

    private static (DocumentEditorShell Guscio, List<string> Ridisegni) Guscio(
        IEditingService editing, TimeSpan? attesaMassimaDelTurno = null)
    {
        var ridisegni = new List<string>();
        var guscio = new DocumentEditorShell(
            editing, new NoJs(), new KeyLocalizer(), NullLogger.Instance,
            famiglia: "prova", chiaveNoPermesso: "Ed_NoPermission",
            ridisegna: () => { ridisegni.Add("ridisegna"); return Task.CompletedTask; },
            attesaMassimaDelTurno: attesaMassimaDelTurno)
        {
            DocumentId = 7,
            IsEditing = true,
        };
        return (guscio, ridisegni);
    }

    /// <summary>
    /// ⚠️ <b>La segnalazione dal campo, riprodotta.</b> Uscire dalla modifica fa DUE cose: rilascia il lock e
    /// poi rilegge com'è rimasto. La seconda stava fuori dal guardiano, quindi una sua eccezione — una corsa
    /// sul DbContext, un guasto passeggero del database — non veniva presa da nessuno: usciva dal gestore
    /// dell'evento, abbatteva il circuito Blazor, e a schermo restava una pagina che non risponde più. Il
    /// lavoro era già salvato (i gesti salvano uno per uno), quindi ricaricare «lo faceva salvare».
    /// </summary>
    [Fact]
    public async Task Uscire_dalla_modifica_non_lascia_scappare_l_eccezione()
    {
        var (guscio, _) = Guscio(new EditingFinto
        {
            SullIspezione = _ => throw new InvalidOperationException("A second operation was started on this context."),
        });

        // Non deve sollevare: un'eccezione qui non la prende più nessuno.
        await guscio.FinishEditingAsync();

        Assert.NotNull(guscio.Error);
        // E si esce comunque dalla modifica: il lock è stato rilasciato, restare «in modifica» sarebbe una bugia.
        Assert.False(guscio.IsEditing);
    }

    /// <summary>
    /// 🔴 <b>Lasciare la pagina mentre un caricamento è in volo non deve abbattere il circuito.</b>
    ///
    /// <para>Segnalazione dal campo del 4 settembre 2026 — «mi compare un pop-up che chiede di ricaricare, e
    /// poi non funziona più niente» — e nel file degli errori di produzione cinque
    /// <c>CircuitUnhandledException</c> in un'ora, tutte
    /// <c>ObjectDisposedException: SemaphoreSlim</c> alla <c>Release</c> dentro <c>InFilaAsync</c>. La
    /// sequenza è questa: la pagina si smonta e smaltisce il guscio, ma l'azione già in coda arriva al suo
    /// <c>finally</c> dopo — e quell'eccezione, sollevata dalla continuazione di un <c>Task</c> che nessuno
    /// aspetta più, non la cattura nessuno.</para>
    ///
    /// <para>⚠️ Il test smaltisce <b>mentre</b> l'azione è in volo, che è l'unico ordine in cui il difetto
    /// esiste: smaltire prima o dopo non lo riproduce.</para>
    /// </summary>
    [Fact]
    public async Task Smaltire_il_guscio_mentre_un_azione_e_in_volo_non_solleva()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var dentro = new TaskCompletionSource();
        var libera = new TaskCompletionSource();

        var inVolo = guscio.InFilaAsync(async () =>
        {
            dentro.SetResult();
            await libera.Task;
        });

        await dentro.Task;          // l'azione è dentro il tornello
        guscio.Dispose();           // la pagina se ne va
        libera.SetResult();         // e solo adesso l'azione finisce, sul guscio smaltito

        await inVolo;               // senza la correzione: ObjectDisposedException, e il circuito muore
    }

    /// <summary>Il caso normale non cambia: lock rilasciato, stato riletto, fuori dalla modifica.</summary>
    [Fact]
    public async Task Uscire_dalla_modifica_rilascia_il_lock_e_chiude()
    {
        var editing = new EditingFinto();
        var (guscio, _) = Guscio(editing);

        await guscio.FinishEditingAsync();

        Assert.Equal(1, editing.RilasciChiesti);
        Assert.False(guscio.IsEditing);
        Assert.Null(guscio.Error);
    }

    /// <summary>
    /// ⚠️ <b>Il badge deve tornare indietro DA SOLO.</b> Il guardiano accende «Salvataggio…» e chiede il
    /// ridisegno; quando l'azione fallisce rimette lo stato a riposo ma il ridisegno non lo chiedeva più, e
    /// contava sul render automatico dell'evento — che ridisegna il componente che l'evento l'ha ricevuto,
    /// non per forza la pagina che disegna il badge. Un gesto nato dentro un componente figlio (l'allegato,
    /// l'immagine, gli editor strutturati) lasciava quindi il badge inchiodato e il messaggio d'errore
    /// invisibile: esattamente il sintomo segnalato.
    /// </summary>
    [Fact]
    public async Task Un_gesto_fallito_riporta_il_badge_a_riposo_E_chiede_il_ridisegno()
    {
        var (guscio, ridisegni) = Guscio(new EditingFinto());

        await guscio.GuardAsync(() => throw new InvalidOperationException("niente da fare"));

        Assert.Equal(SaveState.Idle, guscio.Save);
        Assert.Equal("niente da fare", guscio.Error);
        // Due: uno per accendere «Salvataggio…», uno per mostrare l'errore e spegnerlo.
        Assert.Equal(2, ridisegni.Count);
    }

    /// <summary>E il gesto riuscito lo chiede lo stesso: «Salvato» deve arrivare a chi lo disegna.</summary>
    [Fact]
    public async Task Un_gesto_riuscito_chiede_il_ridisegno()
    {
        var (guscio, ridisegni) = Guscio(new EditingFinto());

        await guscio.GuardAsync(() => Task.CompletedTask);

        Assert.Equal(SaveState.Saved, guscio.Save);
        Assert.Equal(2, ridisegni.Count);
    }

    /// <summary>
    /// Un gesto SILENZIOSO non tocca il badge — «Salvato» su un lock preso sarebbe una bugia — ma quando
    /// fallisce il messaggio deve arrivare a schermo lo stesso.
    /// </summary>
    [Fact]
    public async Task Anche_un_gesto_silenzioso_fallito_chiede_il_ridisegno()
    {
        var (guscio, ridisegni) = Guscio(new EditingFinto());

        await guscio.GuardCoreAsync(() => throw new InvalidOperationException("rotto"), silenziosa: true);

        Assert.Equal(SaveState.Idle, guscio.Save);
        Assert.Equal("rotto", guscio.Error);
        Assert.Single(ridisegni);   // niente badge da accendere: resta il ridisegno dell'errore
    }

    // ---- il tornello: una operazione per volta su questo contesto -------------------------------------

    /// <summary>
    /// ⚠️ <b>La segnalazione del 2 settembre 2026, riprodotta.</b> Aggiungendo una sotto-sezione tornava
    /// «A second operation was started on this context»: non un servizio iniettato male, ma <b>due catene
    /// di caricamento della stessa pagina</b> sovrapposte — un gesto che ricarica cede al primo
    /// <c>await</c>, il ridisegno che segue fa scattare <c>OnParametersSetAsync</c>, che ricarica di nuovo.
    /// Qui il secondo <b>aspetta</b> invece di partire in parallelo.
    /// </summary>
    [Fact]
    public async Task Due_caricamenti_insieme_non_si_sovrappongono()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var dentro = 0;
        var massimoInsieme = 0;
        var apri = new TaskCompletionSource();

        async Task Lento()
        {
            var quanti = Interlocked.Increment(ref dentro);
            massimoInsieme = Math.Max(massimoInsieme, quanti);
            await apri.Task;
            Interlocked.Decrement(ref dentro);
        }

        var primo = guscio.InFilaAsync(Lento);
        var secondo = guscio.InFilaAsync(Lento);

        Assert.False(secondo.IsCompleted);      // il secondo e' in coda, non in volo
        apri.SetResult();
        await Task.WhenAll(primo, secondo);

        Assert.Equal(1, massimoInsieme);
    }

    /// <summary>
    /// ⚠️ E il tornello <b>non deve chiudersi in faccia a se stesso</b>: le catene si annidano davvero —
    /// «inizia modifica» è un gesto (in fila) che chiama il ricarico della pagina (in fila). Senza la
    /// memoria del flusso corrente l'editor si pianterebbe invece di morire, che è peggio: sembra lentezza.
    /// </summary>
    [Fact]
    public async Task Una_catena_annidata_non_aspetta_se_stessa()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var passi = new List<string>();

        var lavoro = guscio.InFilaAsync(async () =>
        {
            passi.Add("fuori");
            await guscio.InFilaAsync(() => { passi.Add("dentro"); return Task.CompletedTask; });
            passi.Add("fine");
        });

        await lavoro.WaitAsync(TimeSpan.FromSeconds(5));   // senza la guardia, qui si aspetterebbe per sempre
        Assert.Equal(new[] { "fuori", "dentro", "fine" }, passi);
    }

    /// <summary>
    /// 🔴 <b>Il difetto del 9 settembre 2026, in un editor UNITO con tre membri</b> (segnalato dal
    /// committente su LIBV; diagnostica di produzione delle 14:05-14:11).
    ///
    /// <para>Il tornello ricorda di essere «già dentro» con un <c>AsyncLocal</c>, e un <c>AsyncLocal</c>
    /// <b>si eredita</b>: ogni flusso che nasce dentro un'operazione in fila lo trova acceso. Un render
    /// provocato da lì dentro chiama <c>OnParametersSetAsync</c> del componente, che legge «sono già
    /// dentro» e <b>salta la coda</b> — partendo <b>accanto</b> alla prima invece che dopo. Due catene
    /// sullo stesso <c>DbContext</c>: «A second operation was started», il render muore a metà, e da lì in
    /// poi il diff di Blazor è corrotto e la pagina non risponde più a niente.</para>
    ///
    /// <para>⚠️ <b>La differenza con <c>Una_catena_annidata_non_aspetta_se_stessa</c> è UNA sola, e
    /// è tutta</b>: lì la catena interna viene <b>attesa</b> da quella esterna — sono la stessa catena, e
    /// scavalcare è giusto, o si aspetterebbe se stessi. Qui <b>non viene attesa</b>: la tiene il
    /// renderer. Sono due catene, e la seconda deve mettersi in coda.</para>
    ///
    /// <para>⚠️ Perché non si vede in locale: è una corsa. Con un editor solo la finestra è stretta;
    /// con tre membri l'ospite ridisegna a ogni caricamento, e su MariaDB le query durano abbastanza da
    /// farle sovrapporre. Su SQLite finiscono prima che la seconda parta.</para>
    /// </summary>
    [Fact]
    public async Task Un_caricamento_provocato_DA_DENTRO_si_mette_in_coda()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var dentro = 0;
        var massimoInsieme = 0;
        var apri = new TaskCompletionSource();

        async Task Lento()
        {
            var quanti = Interlocked.Increment(ref dentro);
            massimoInsieme = Math.Max(massimoInsieme, quanti);
            await apri.Task;
            Interlocked.Decrement(ref dentro);
        }

        Task? intruso = null;
        var primo = guscio.InFilaAsync(async () =>
        {
            // Il render provocato da qui dentro: il componente chiama OnParametersSetAsync, e quel Task
            // NON lo attende chi lo ha provocato — lo tiene il renderer. E' l'unico dettaglio che conta.
            intruso = guscio.CaricaInFilaAsync(Lento);
            await Lento();
        });

        apri.SetResult();
        await Task.WhenAll(primo, intruso!).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, massimoInsieme);
    }

    /// <summary>
    /// 🔴 <b>Chi ha aspettato il turno riparte sul DISPATCHER, non sul pool</b> (§CW, 11 settembre 2026).
    ///
    /// <para>La NRE di render dell'editor APP, cercata dal 7 settembre, l'ha spiegata la rete di contesto di
    /// 1.18.2: nello stesso disegno <c>_shell.Doc</c> era pieno alla riga che sceglie il ramo e <b>nullo</b>
    /// due righe dopo («documento=NON caricato»). Nessun thread del renderer può farlo: lo faceva il secondo
    /// caricamento, che aspettava il tornello con <c>ConfigureAwait(false)</c> e quindi ripartiva <b>sul
    /// pool</b> — e la prima riga di <c>ParametriAsync</c> è <c>_shell.Doc = null</c>, scritta mentre il
    /// dispatcher disegnava il seguito del primo (<c>CallStateHasChangedOnAsyncCompletion</c>).</para>
    ///
    /// <para>⚠️ Serve un dispatcher vero per vederlo: senza <see cref="SynchronizationContext"/> ogni seguito
    /// finisce sul pool comunque, e il test passerebbe anche col difetto.</para>
    /// </summary>
    [Fact]
    public async Task Chi_aspetta_il_turno_riparte_sul_dispatcher()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        using var dispatcher = new DispatcherDiProva();
        var apri = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int? threadDellAzione = null;
        Task primo = Task.CompletedTask, secondo = Task.CompletedTask;

        await dispatcher.Esegui(() =>
        {
            // Il primo tiene il tornello; il secondo — il caricamento che il renderer fa ripartire — aspetta.
            primo = guscio.InFilaAsync(() => apri.Task);
            secondo = guscio.CaricaInFilaAsync(() =>
            {
                threadDellAzione = Environment.CurrentManagedThreadId;
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        });

        Assert.False(secondo.IsCompleted);   // il secondo è davvero in attesa del turno
        apri.SetResult();
        await Task.WhenAll(primo, secondo).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(dispatcher.ThreadId, threadDellAzione);
    }

    /// <summary>Lo stesso per un <b>gesto</b>: passa da <c>InFilaAsync</c>, che ha la sua attesa.</summary>
    [Fact]
    public async Task Anche_un_gesto_che_aspetta_il_turno_riparte_sul_dispatcher()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        using var dispatcher = new DispatcherDiProva();
        var apri = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int? threadDelGesto = null;
        Task primo = Task.CompletedTask, gesto = Task.CompletedTask;

        await dispatcher.Esegui(() =>
        {
            primo = guscio.CaricaInFilaAsync(() => apri.Task);
            gesto = guscio.GuardAsync(() =>
            {
                threadDelGesto = Environment.CurrentManagedThreadId;
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        });

        Assert.False(gesto.IsCompleted);
        apri.SetResult();
        await Task.WhenAll(primo, gesto).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(dispatcher.ThreadId, threadDelGesto);
    }

    /// <summary>Anche i GESTI passano dal tornello: due salvataggi a raffica sono due catene sullo stesso
    /// contesto quanto lo sono un gesto e un ricarico.</summary>
    [Fact]
    public async Task Anche_i_gesti_stanno_in_fila()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var insieme = 0;
        var massimo = 0;
        var apri = new TaskCompletionSource();

        async Task Gesto()
        {
            massimo = Math.Max(massimo, Interlocked.Increment(ref insieme));
            await apri.Task;
            Interlocked.Decrement(ref insieme);
        }

        var a = guscio.GuardAsync(Gesto);
        var b = guscio.GuardAsync(Gesto);
        apri.SetResult();
        await Task.WhenAll(a, b);

        Assert.Equal(1, massimo);
    }

    /// <summary>
    /// 🔴 <b>La coppia delle 16:51:13 del 7 settembre 2026, in miniatura.</b> Chiudere la pagina mentre un
    /// caricamento è in volo chiudeva lo scope di DI — e con lui il <c>DbContext</c> e la sua connessione —
    /// <b>sotto</b> una query ancora aperta. Nel registro di produzione sono trentanove
    /// <c>ObjectDisposedException</c> lette come rumore, e una sessione MySQL restituita al pool con una
    /// lettura in corso: la prende poi un altro circuito, e lì diventa «<c>another read operation is
    /// pending</c>» sul socket.
    ///
    /// <para>Qui si prova la sola cosa che il guscio può garantire: <c>ChiudiAsync</c> <b>non torna</b>
    /// finché chi era dentro il tornello non è uscito. Chi chiama smaltisce lo scope dopo, e quel «dopo» è
    /// tutta la correzione.</para>
    /// </summary>
    [Fact]
    public async Task Chiudere_aspetta_il_caricamento_in_volo()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        var apri = new TaskCompletionSource();
        var finito = false;

        var caricamento = guscio.InFilaAsync(async () => { await apri.Task; finito = true; });

        var chiusura = guscio.ChiudiAsync();
        Assert.False(chiusura.IsCompleted);   // senza l'attesa, qui lo scope sarebbe già chiuso
        Assert.False(finito);

        apri.SetResult();
        await chiusura.WaitAsync(TimeSpan.FromSeconds(5));
        await caricamento;
        Assert.True(finito);
    }

    /// <summary>
    /// ⚠️ E dopo la chiusura <b>non si comincia niente di nuovo</b>: una catena che partisse adesso
    /// andrebbe a sbattere sul <c>DbContext</c> che sta per essere smaltito, cioè rifarebbe il difetto
    /// dall'altro capo. Chi arriva tardi se ne torna <b>subito</b>: non aspetta un tornello che non si
    /// riaprirà, che sarebbe un task fermo per sempre.
    /// </summary>
    [Fact]
    public async Task Dopo_la_chiusura_non_si_comincia_piu_niente()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        await guscio.ChiudiAsync();

        var partito = false;
        var tardivo = guscio.InFilaAsync(() => { partito = true; return Task.CompletedTask; });

        await tardivo.WaitAsync(TimeSpan.FromSeconds(5));   // torna, e torna subito
        Assert.False(partito);
    }

    /// <summary>Chiudere due volte è lecito: lo smontaggio di un componente non è un gesto che si conta.</summary>
    [Fact]
    public async Task Chiudere_due_volte_non_solleva()
    {
        var (guscio, _) = Guscio(new EditingFinto());
        await guscio.ChiudiAsync();
        await guscio.ChiudiAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// 🔴 Un turno che non arriva NON resta appeso per sempre, e chi lo aspettava lo viene a sapere.
    ///
    /// <para><b>Perché esiste.</b> Il 9 settembre 2026 la ✕ del pannello dell'unione ha smesso di aprire
    /// perfino la propria conferma. Non era il tasto: <c>UnionPanel.EseguiAsync</c> accende <c>_busy</c>,
    /// attende <c>Changed</c> — che finisce in questo tornello — e lo spegne nel <c>finally</c>. Con
    /// un'attesa senza tetto quel <c>finally</c> non gira mai: <c>_busy</c> resta acceso e TUTTI i comandi
    /// dell'unione restano <c>disabled</c>. Un guasto muto travestito da tasto rotto.</para>
    ///
    /// <para>⚠️ La prova <b>distingue</b>: sul codice di prima non fallisce con un'asserzione, si pianta —
    /// ed è per questo che il <c>WaitAsync</c> di sicurezza sta sul gesto e non sull'assert.</para>
    /// </summary>
    [Fact]
    public async Task Un_gesto_che_non_ottiene_il_turno_SOLLEVA_invece_di_restare_appeso()
    {
        var (guscio, _) = Guscio(new EditingFinto(), attesaMassimaDelTurno: TimeSpan.FromMilliseconds(150));
        var tieniIlTurno = new TaskCompletionSource();

        // Chi sta davanti e non molla: è il caricamento che non torna.
        var primo = guscio.InFilaAsync(() => tieniIlTurno.Task);

        // Il gesto che arriva dopo. Senza tetto resterebbe qui per sempre.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => guscio.GuardCoreAsync(() => Task.CompletedTask, silenziosa: true)
                        .WaitAsync(TimeSpan.FromSeconds(5)));

        tieniIlTurno.SetResult();
        await primo.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// ⚠️ E un CARICAMENTO no: rinuncia al giro in silenzio verso l'utente (ma non verso il log).
    ///
    /// <para>🔴 <b>Le due porte non si comportano allo stesso modo, ed è voluto.</b> Il <c>Task</c> di
    /// <c>OnParametersSetAsync</c> lo tiene il renderer, e nessuno lo attende: sollevare lì è un'eccezione
    /// non catturata nel ciclo di vita, cioè <b>il circuito abbattuto per un ritardo</b> — esattamente il
    /// guasto che si sta togliendo. Il render successivo riprova da sé.</para>
    /// </summary>
    [Fact]
    public async Task Un_caricamento_che_non_ottiene_il_turno_RINUNCIA_senza_sollevare()
    {
        var (guscio, _) = Guscio(new EditingFinto(), attesaMassimaDelTurno: TimeSpan.FromMilliseconds(150));
        var tieniIlTurno = new TaskCompletionSource();
        var primo = guscio.InFilaAsync(() => tieniIlTurno.Task);

        var partito = false;
        var caricamento = guscio.CaricaInFilaAsync(() => { partito = true; return Task.CompletedTask; });

        await caricamento.WaitAsync(TimeSpan.FromSeconds(5));   // torna, e non solleva
        Assert.False(partito);                                   // e non è partito accanto al primo

        tieniIlTurno.SetResult();
        await primo.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
