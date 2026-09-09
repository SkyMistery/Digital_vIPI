using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>Stato del badge di salvataggio in cima all'editor.</summary>
public enum SaveState { Idle, Saving, Saved }

/// <summary>
/// Il guscio comune dei quattro editor documentali (doc 14 §3d): stato della schermata (documento, modifica in
/// corso, errore, lock, badge di salvataggio) e i gesti che quello stato lo muovono — prendere il lock, aprire
/// una bozza, eseguire un'azione mostrandone l'esito, aprire e chiudere tutte le sezioni.
/// <para>
/// ⚠️ Perché esiste. I quattro editor avevano <b>sedici membri privati con lo stesso nome</b>, e i corpi
/// combaciavano dall'83% al 100%: <c>FinishEditing</c> era identico in tre su quattro, <c>StartEditing</c> in
/// due, <c>GuardCore</c> divergeva solo per la chiave di traduzione e la parola nel log. Erano quattro copie
/// della stessa idea, e ogni decisione sull'editing andava presa quattro volte — o, più spesso, tre.
/// </para>
/// <para>
/// ⚠️ Perché è una CLASSE e non un componente. I quattro editor hanno impaginazioni diverse — l'ACC costruisce
/// la propria griglia e monta un <c>DocumentSectionsEditor</c> per blocco, gli altri ne montano uno solo — e
/// infilarli in un guscio visuale comune sarebbe un secondo refactor travestito da primo. Qui si condivide il
/// comportamento; il layout resta di ciascuno.
/// </para>
/// <para>
/// La pagina lo costruisce in <c>OnInitialized</c>, gli passa i propri servizi e due funzioni: come farsi
/// ridisegnare e come ricaricarsi. Tutto il resto lo fa il guscio.
/// </para>
/// </summary>
public sealed class DocumentEditorShell : IDisposable
{
    private readonly IEditingService _editing;
    private readonly IJSRuntime _js;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly ILogger _log;
    private readonly string _famiglia;
    private readonly string _chiaveNoPermesso;
    private readonly Func<Task> _ridisegna;
    private readonly DelayedUiAction _spegniSalvato = new();

    /// <summary>
    /// Il <b>tornello</b>: sul contesto di questa pagina passa <b>una operazione per volta</b>.
    ///
    /// <para>⚠️ Il 2 settembre 2026, aggiungendo una sotto-sezione alle Separazioni della vIPI di LIBB, è
    /// ricomparso «A second operation was started on this context». Non era un servizio iniettato male:
    /// erano <b>due catene di caricamento della stessa pagina</b> sovrapposte. Un gesto chiama
    /// <c>OnChanged</c> → il ricarico parte e <b>cede</b> al primo <c>await</c>; il ridisegno che segue fa
    /// scattare <c>OnParametersSetAsync</c>, che ricarica <b>di nuovo</b>. Due catene, lo stesso
    /// <c>DbContext</c>, e chi arriva secondo muore — portandosi via il circuito, perché un'eccezione nel
    /// ciclo di vita non la cattura nessuno.</para>
    ///
    /// <para>⚠️ <b>Non si risolve isolando altri servizi.</b> Il pannello release <b>deve</b> condividere il
    /// contesto della pagina — il publish è un'operazione sola composta con <c>BeforePublishAsync</c>, e
    /// spezzarla su due contesti la manda in stallo (sta scritto in testa a <c>ReleasePanel</c>). Qui non si
    /// separano i contesti: si mette in fila chi li usa.</para>
    /// </summary>
    private readonly SemaphoreSlim _tornello = new(1, 1);

    /// <summary>
    /// Vero quando il flusso corrente è <b>già dentro</b> il tornello.
    /// <para>⚠️ Serve perché il tornello non è rientrante e le nostre catene si annidano davvero:
    /// <c>StartEditingAsync</c> è un gesto (in fila) che <b>chiama il ricarico della pagina</b> (in fila).
    /// Senza questa memoria si aspetterebbe se stessi, per sempre: l'editor si pianterebbe invece di
    /// morire — che è peggio, perché sembra lentezza.</para>
    /// </summary>
    private readonly AsyncLocal<bool> _inFila = new();

    /// <summary>Vero dopo <see cref="ChiudiAsync"/>: da lì in poi nel tornello non entra più nessuno.</summary>
    private bool _chiuso;

    /// <summary>
    /// Quanto si aspetta, al massimo, che il caricamento in volo esca dal tornello prima di chiudere lo
    /// scope lo stesso. ⚠️ Un caricamento normale dura millisecondi: questo tetto non è la durata attesa, è
    /// il limite oltre il quale una pagina che non torna più non deve tenere in ostaggio la propria chiusura.
    /// </summary>
    private static readonly TimeSpan AttesaMassimaDiChiusura = TimeSpan.FromSeconds(15);

    /// <param name="famiglia">Come si chiama questo documento nei log: «ACC», «APP», «vLOA», «aeroporto».</param>
    /// <param name="chiaveNoPermesso">Chiave di traduzione del «non hai il permesso» di questa famiglia: è la
    /// sola frase che le quattro non condividono, perché nomina il tipo di documento.</param>
    /// <param name="ridisegna">Come la pagina si fa ridisegnare: <c>() =&gt; InvokeAsync(StateHasChanged)</c>.</param>
    /// <param name="attesaMassimaDelTurno">Solo per i test: il tetto d'attesa al tornello. Null = quello
    /// vero (trenta secondi).</param>
    public DocumentEditorShell(IEditingService editing, IJSRuntime js, IStringLocalizer<SharedResource> l,
        ILogger log, string famiglia, string chiaveNoPermesso, Func<Task> ridisegna,
        TimeSpan? attesaMassimaDelTurno = null)
    {
        _attesaMassimaDelTurno = attesaMassimaDelTurno ?? AttesaPredefinitaDelTurno;
        _editing = editing;
        _js = js;
        _l = l;
        _log = log;
        _famiglia = famiglia;
        _chiaveNoPermesso = chiaveNoPermesso;
        _ridisegna = ridisegna;
    }

    // ---- stato della schermata ----

    /// <summary>Il documento in lavorazione. Lo carica la pagina: quale documento sia è la sola cosa per-tipo.</summary>
    public EditableDocument? Doc { get; set; }

    /// <summary>Id del documento, quando esiste. Null = non ancora creato.</summary>
    public int? DocumentId { get; set; }

    /// <summary>Modifica in corso: il lock è nostro e la bozza è aperta.</summary>
    public bool IsEditing { get; set; }

    /// <summary>L'ultimo errore da mostrare in cima, o null.</summary>
    public string? Error { get; set; }

    /// <summary>Stato del lock di editing sul documento.</summary>
    public LockInfo Lock { get; set; } = LockInfo.Free();

    /// <summary>Badge di salvataggio.</summary>
    public SaveState Save { get; private set; }

    /// <summary>Vero quando il JavaScript della pagina è pronto: prima, chiamarlo solleva.</summary>
    public bool JsReady { get; set; }

    // ---- gesti ----

    /// <summary>
    /// Esegue un'azione mostrandone l'esito: badge «salvataggio…», poi «salvato» che si spegne da solo, e gli
    /// errori tradotti al posto di un circuito caduto.
    /// </summary>
    /// <summary>
    /// Esegue <paramref name="azione"/> <b>una per volta</b> su questa pagina. Ci passano i gesti (dal
    /// guardiano) e i <b>caricamenti</b> — che non sono gesti, e sono l'altra metà della corsa.
    /// <para>Chi è già in fila non si rimette in coda: eseguirebbe l'attesa di se stesso.</para>
    /// </summary>
    public async Task InFilaAsync(Func<Task> azione)
    {
        // ⚠️ La pagina se n'è andata: non si comincia niente di nuovo. Non è una cortesia — dopo
        // `ChiudiAsync` lo scope di DI sta per chiudersi, e una catena che partisse adesso andrebbe a
        // sbattere sul `DbContext` smaltito, cioè esattamente il difetto che ChiudiAsync esiste per togliere.
        if (_chiuso) return;
        if (_inFila.Value) { await azione(); return; }
        await CodaAsync(azione, chiSpettaLoVede: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Quanto si aspetta, al massimo, il proprio turno al tornello.
    ///
    /// <para>🔴 <b>Perché c'è un tetto, dal 9 settembre 2026.</b> Questa attesa non ne aveva, mentre
    /// <see cref="ChiudiAsync"/> due metodi più sotto ce l'ha da sempre: la stessa attesa con due regole
    /// diverse, e quella senza guardia è quella che si è piantata. Un turno che non arriva mai non è un
    /// caricamento lento — è un gesto che non torna, e chi lo aspetta resta fermo <b>senza una riga di
    /// log</b>. Sul pannello dell'unione si vedeva così: <c>UnionPanel.EseguiAsync</c> attende
    /// <c>Changed</c>, che finisce qui dentro; se non torna, il suo <c>finally</c> non gira, <c>_busy</c>
    /// resta acceso e <b>tutti</b> i comandi dell'unione restano <c>disabled</c> — la ✕ non apre nemmeno la
    /// conferma. Un guasto muto travestito da tasto rotto.</para>
    ///
    /// <para>⚠️ Trenta secondi e non quindici: è il <c>DefaultCommandTimeout</c> della connessione, cioè il
    /// tempo oltre il quale chi sta davanti non sta più lavorando ma è appeso.</para>
    /// </summary>
    private static readonly TimeSpan AttesaPredefinitaDelTurno = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Il tetto vero di questa istanza. ⚠️ È un parametro <b>facoltativo del costruttore</b> e non una
    /// costante, per una ragione sola: un tetto di trenta secondi non si può provare su un banco: la prova
    /// durerebbe trenta secondi o non ci sarebbe. Le cinque pagine non lo passano e prendono il valore vero.
    /// </summary>
    private readonly TimeSpan _attesaMassimaDelTurno;

    /// <summary>
    /// Il tornello vero e proprio, senza la corsia dei rientri.
    ///
    /// <para>⚠️ <b>Scaduto il tetto le due porte si comportano in modo diverso, ed è voluto.</b> Un
    /// <b>gesto</b> (<paramref name="chiSpettaLoVede"/> vero) ha qualcuno che ne aspetta l'esito: solleva,
    /// così l'errore si vede in pagina, il badge torna indietro e il <c>finally</c> di chi ha premuto gira —
    /// cioè i tasti si riaccendono. Un <b>caricamento</b> no: lo tiene il renderer, e sollevare lì
    /// abbatterebbe il circuito per un ritardo. Quello rinuncia al giro e lo scrive; il render successivo
    /// riprova da sé, perché <c>OnParametersSetAsync</c> scatta di nuovo.</para>
    /// </summary>
    private async Task CodaAsync(Func<Task> azione, bool chiSpettaLoVede)
    {
        if (_chiuso) return;
        if (!await _tornello.WaitAsync(_attesaMassimaDelTurno).ConfigureAwait(false))
        {
            _log.LogError(
                "Tornello dell'editor {Famiglia}: turno non arrivato dopo {Secondi}s (documento {DocId}, {Chi}).",
                _famiglia, _attesaMassimaDelTurno.TotalSeconds, DocumentId,
                chiSpettaLoVede ? "gesto" : "caricamento");
            if (!chiSpettaLoVede) return;
            throw new InvalidOperationException(
                $"L'editor {_famiglia} non ha ottenuto il proprio turno entro {_attesaMassimaDelTurno.TotalSeconds:0} secondi: " +
                "un'operazione precedente non è ancora tornata. Ricaricare la pagina.");
        }
        _inFila.Value = true;
        try { await azione(); }
        finally { _inFila.Value = false; _tornello.Release(); }
    }

    /// <summary>
    /// Il caricamento che <c>OnParametersSetAsync</c> fa partire: <b>si mette SEMPRE in coda</b>, anche se
    /// il flusso corrente risulta già dentro il tornello.
    ///
    /// <para>🔴 <b>Perché esiste una porta a parte.</b> La memoria di «sono già dentro» è un
    /// <see cref="AsyncLocal{T}"/>, e un <c>AsyncLocal</c> <b>si eredita</b>: ogni flusso nato dentro
    /// un'operazione in fila lo trova acceso. Un render provocato da lì dentro chiama
    /// <c>OnParametersSetAsync</c>, che leggeva «sono già dentro» e <b>scavalcava la coda</b> — partendo
    /// <b>accanto</b> alla prima invece che dopo. Due catene sullo stesso <c>DbContext</c>.</para>
    ///
    /// <para>⚠️ <b>La differenza con una catena annidata è chi ATTENDE.</b> <c>StartEditingAsync</c>
    /// chiama il ricarico e lo <b>aspetta</b>: sono la stessa catena, e scavalcare è giusto, o si
    /// aspetterebbe se stessi. Il <c>Task</c> di <c>OnParametersSetAsync</c> non lo aspetta chi lo ha
    /// provocato — lo tiene il renderer: sono due catene, e la seconda va in coda. Non c'è stallo proprio
    /// per quel motivo.</para>
    ///
    /// <para>Visto in produzione il 9 settembre 2026 su un editor UNITO a tre membri (LIBV): «A second
    /// operation was started», poi il render morto a metà e il diff di Blazor corrotto — da lì in poi la
    /// pagina non risponde più a niente, nemmeno a «sciogli l'unione». Inchiodato da
    /// <c>Un_caricamento_provocato_DA_DENTRO_si_mette_in_coda</c>.</para>
    /// </summary>
    public Task CaricaInFilaAsync(Func<Task> azione) => CodaAsync(azione, chiSpettaLoVede: false);

    public Task GuardAsync(Func<Task> azione) => GuardCoreAsync(azione, silenziosa: false);

    /// <summary>
    /// Come <see cref="GuardAsync"/>, ma senza badge: per i gesti che contenuto non ne salvano — prendere o
    /// rilasciare il lock — dove «Salvato» sarebbe una bugia. Gli errori si gestiscono lo stesso.
    /// </summary>
    public Task GuardCoreAsync(Func<Task> azione, bool silenziosa) => EseguiAsync(azione, silenziosa);

    /// <summary>
    /// Come <see cref="GuardAsync"/>, ma dice se è andata. Serve a chi deve decidere qualcosa dopo — l'editor
    /// d'aeroporto spunta la sezione dalle «non salvate» solo se il salvataggio è riuscito davvero.
    /// <para>
    /// ⚠️ Era una QUINTA copia, dentro l'editor d'aeroporto e chiamata <c>Guarded</c>: stessa idea, stessi
    /// catch, e una differenza sola — mostrava il messaggio grezzo di <c>EditNotAllowedException</c> invece
    /// della frase tradotta. Quel messaggio è una stringa italiana fissa dentro l'eccezione, quindi a un
    /// lettore inglese usciva in italiano: unificando si guadagna anche quello.
    /// </para>
    /// </summary>
    public Task<bool> GuardedAsync(Func<Task> azione) => EseguiAsync(azione, silenziosa: false);

    private async Task<bool> EseguiAsync(Func<Task> azione, bool silenziosa)
    {
        // ⚠️ Anche i gesti passano dal tornello: due salvataggi lanciati a raffica sono due catene sullo
        // stesso contesto quanto lo sono un gesto e un ricarico.
        var esito = false;
        await InFilaAsync(async () => esito = await EseguiCoreAsync(azione, silenziosa));
        return esito;
    }

    private async Task<bool> EseguiCoreAsync(Func<Task> azione, bool silenziosa)
    {
        Error = null;
        if (!silenziosa) { Save = SaveState.Saving; await _ridisegna(); }
        try
        {
            await azione();
            if (!silenziosa)
            {
                Save = SaveState.Saved;
                // Il badge «Salvato» si spegne da solo. Con DelayedUiAction, che annulla il precedente e si
                // ferma anche allo smontaggio: senza, chi salvava e cambiava pagina entro due secondi lasciava
                // un ridisegno su un renderer che non c'era più, dentro un task che nessuno osserva.
                _spegniSalvato.Schedule(TimeSpan.FromSeconds(2), () =>
                {
                    if (Save != SaveState.Saved) return Task.CompletedTask;
                    Save = SaveState.Idle;
                    return _ridisegna();
                });
            }
            return true;
        }
        catch (EditNotAllowedException) { Error = _l[_chiaveNoPermesso]; Save = SaveState.Idle; }
        catch (EditConflictException ex)
        {
            Error = ex.Message;
            Save = SaveState.Idle;
            // Qualcun altro ha il lock: si rilegge chi, o la barra resterebbe a dire che è nostro.
            // ⚠️ E se anche QUESTA lettura fallisce non se ne fa niente: siamo già dentro la gestione di un
            // guasto, e un'eccezione sollevata da un `catch` esce dal guardiano intatta — abbatte il circuito
            // proprio mentre stavamo scrivendo all'utente che cos'era andato storto. Il nome di chi tiene il
            // lock è la parte che si può perdere.
            if (DocumentId is int id)
            {
                try { Lock = await _editing.InspectLockAsync(id); }
                catch (Exception letturaLock)
                {
                    _log.LogWarning(letturaLock, "Rilettura del lock fallita dopo un conflitto (documento {DocId}).", id);
                }
            }
        }
        catch (Vipi.Application.Aor.ValidationException ex) { Error = ex.Message; Save = SaveState.Idle; }
        // I salvataggi di dati strutturati dell'aeroporto la usano per dire «questo stato non si può salvare».
        catch (InvalidOperationException ex) { Error = ex.Message; Save = SaveState.Idle; }
        // Rete di sicurezza: senza, un'eccezione non prevista (DbUpdateException, Npgsql…) abbatteva il
        // circuito e lasciava il badge inchiodato su «Salvataggio». Meglio un errore visibile.
        catch (Exception ex)
        {
            Error = _l["Common_UnexpectedError"] + ex.Message;
            Save = SaveState.Idle;
            _log.LogError(ex, "Azione editor {Famiglia} fallita (documento {DocId}).", _famiglia, DocumentId);
        }
        finally
        {
            // ⚠️ IL RIDISEGNO SI CHIEDE SEMPRE, e prima non si chiedeva: si accendeva «Salvataggio…» e poi si
            // contava sul render automatico dell'evento per farlo tornare indietro. Quel render ridisegna il
            // componente che l'evento l'ha RICEVUTO — e il badge, come il messaggio d'errore, li disegna la
            // PAGINA. Un gesto nato dentro un componente figlio (il blocco allegato, l'immagine, gli editor
            // strutturati) lasciava quindi il badge inchiodato su «Salvataggio…» e l'errore invisibile: a
            // schermo la pagina sembrava bloccata, e l'unica via d'uscita era ricaricarla — anche se il
            // lavoro era già salvato. Segnalato dal campo il 1 settembre 2026.
            await _ridisegna();
        }
        return false;
    }

    /// <summary>
    /// Entra in modifica. Una versione pubblicata non si tocca: si apre una BOZZA — è la regola di tutte e
    /// quattro le famiglie. Su una bozza già aperta basta prendere il lock.
    /// </summary>
    public async Task StartEditingAsync(Func<Task> ricarica)
    {
        if (DocumentId is not int id) return;
        await GuardCoreAsync(async () =>
        {
            if (Doc is { IsEditable: false }) await _editing.CreateDraftAsync(id);
            else await _editing.AcquireLockAsync(id);
            await ricarica();
            IsEditing = Doc?.IsEditable == true;
        }, silenziosa: true);
    }

    /// <summary>
    /// Esce dalla modifica e molla il lock, così un altro editore può entrare senza aspettarne la scadenza.
    ///
    /// <para>
    /// ⚠️ <b>TUTTO dentro il guardiano, riletura del lock compresa.</b> Prima la rilettura stava fuori, ed era
    /// l'unico <c>await</c> non protetto di tutta la classe: una sua eccezione — una corsa sul DbContext del
    /// circuito, un guasto passeggero del database — non la prendeva nessuno, usciva dal gestore dell'evento e
    /// abbatteva il circuito Blazor. A schermo: si preme «Fine modifica» e la pagina non risponde più, senza
    /// un errore, con il badge fermo sull'ultimo salvataggio. Il lavoro era al sicuro — i gesti dell'editor
    /// salvano uno per uno — quindi bastava ricaricare, ed è così che la segnalazione è arrivata: «si blocca
    /// in salvataggio e si deve ricaricare la pagina per farla salvare» (1 settembre 2026).
    /// </para>
    ///
    /// <para>
    /// ⚠️ E <see cref="IsEditing"/> si spegne <b>comunque</b>, anche se il rilascio è fallito: restare «in
    /// modifica» dopo aver chiesto di uscire è lo stato peggiore dei tre — chi guarda crede di avere il lock e
    /// continua a scrivere. Se il rilascio non è passato il lock resta nostro e scade da sé; l'errore è a
    /// schermo, e rientrare in modifica è un clic.
    /// </para>
    /// </summary>
    public async Task FinishEditingAsync()
    {
        if (DocumentId is not int id) return;
        await GuardCoreAsync(async () =>
        {
            await _editing.ReleaseLockAsync(id);
            Lock = await _editing.InspectLockAsync(id);
        }, silenziosa: true);
        IsEditing = false;
    }

    /// <summary>
    /// Apre o chiude tutte le sezioni e tutti i blocchi. ⚠️ La guardia è <see cref="JsReady"/> e non un
    /// <c>catch</c>: due dei quattro editor si proteggevano con <c>catch (Exception) { }</c>, che oltre al caso
    /// previsto — JavaScript non ancora pronto — ingoiava in silenzio qualunque altro guasto, per sempre
    /// (invariante #7 del runbook di refactor).
    /// </summary>
    public async Task ToggleAllSectionsAsync(bool aperte)
    {
        if (!JsReady) return;
        await _js.InvokeVoidAsync("vipiEditorSections", aperte);
    }

    /// <summary>
    /// Ferma il badge che si spegne da solo: dopo lo smontaggio non c'è più un renderer da avvisare.
    ///
    /// <para>🔴 <b>Il tornello NON si smaltisce, ed è una correzione pagata in produzione</b> (4 settembre
    /// 2026, cinque `CircuitUnhandledException` in un'ora). Chi lascia la pagina mentre un caricamento è in
    /// volo fa arrivare quel caricamento al suo <c>finally</c> — che chiama <c>Release()</c> — <b>dopo</b>
    /// questo <c>Dispose</c>: <c>ObjectDisposedException: SemaphoreSlim</c>, sollevata dalla continuazione di
    /// un <c>Task</c> che nessuno aspetta più. Non la cattura nessuno, e Blazor risponde nel solo modo che
    /// conosce: <b>abbatte il circuito</b>. A schermo è il riquadro «ricarica la pagina», e da lì in poi non
    /// funziona più niente finché non si ricarica davvero.</para>
    ///
    /// <para>⚠️ <b>Non smaltirlo non perde niente</b>: <see cref="SemaphoreSlim.Dispose()"/> serve solo a chi
    /// ha chiesto <c>AvailableWaitHandle</c>, che qui non tocca nessuno — senza quello il tipo non tiene
    /// risorse non gestite, e il GC lo raccoglie con la pagina. Il rimedio giusto è togliere la riga, non
    /// avvolgere il <c>Release</c> in un <c>try</c>: una <c>Release</c> che fallisce in silenzio lascerebbe
    /// il tornello chiuso, e la pagina dopo si pianterebbe invece di cadere.</para>
    /// </summary>
    public void Dispose() => _spegniSalvato.Dispose();

    /// <summary>
    /// Chiude la pagina: ferma i badge e <b>aspetta che l'operazione in volo esca dal tornello</b>. Solo dopo
    /// chi chiama può smaltire il proprio scope di DI.
    ///
    /// <para>🔴 <b>Perché non basta <see cref="Dispose"/>.</b> Il tornello mette in fila chi lavora, ma non
    /// ha nessuna presa su chi <b>se ne va</b>: fino al 7 settembre 2026 lo smontaggio chiudeva lo scope — e
    /// con lui il <c>DbContext</c> e la sua connessione — mentre una query era ancora aperta. Nel registro di
    /// produzione sono trentanove <c>ObjectDisposedException</c> su <c>VipiDbContext</c>, lette fino a ieri
    /// come rumore («l'utente ha cambiato pagina»).</para>
    ///
    /// <para>🔴 <b>Non sono rumore.</b> Una sessione MySQL restituita al pool con una lettura ancora in corso
    /// la trova poi <b>qualcun altro</b>: è la coppia delle 16:51:13 del 7 settembre — «<c>This method may
    /// not be called when another read operation is pending</c>» sul socket, dalla parte di chi la sessione
    /// l'ha appena presa, e una <c>NullReferenceException</c> dentro <c>MySqlDataReader</c> dalla parte di
    /// chi la stava ancora leggendo. Vedi <c>docs/lavori-aperti.md</c> §CD.</para>
    ///
    /// <para>⚠️ <b>A chiudere la porta è il flag, non il semaforo tenuto stretto.</b> Trattenere il
    /// tornello per sempre avrebbe fatto aspettare <i>all'infinito</i> chi fosse arrivato dopo — un task che
    /// non finisce più, che è il modo silenzioso di perdere memoria. Il flag invece manda via chi arriva
    /// tardi <b>subito</b>, e l'attesa qui sotto riguarda solo chi era già dentro.</para>
    ///
    /// <para>⚠️ Scaduto il tetto si chiude lo stesso. È il comportamento di prima — il male minore già noto —
    /// e non un modo nuovo di piantarsi; ma lo si scrive nel log, perché una chiusura che scade è un
    /// caricamento che non torna, cioè una cosa da guardare.</para>
    /// </summary>
    public async Task ChiudiAsync()
    {
        Dispose();
        // Chi è già dentro il tornello aspetterebbe se stesso — stessa regola di InFilaAsync.
        if (_chiuso || _inFila.Value) return;
        // ⚠️ Prima la porta, poi l'attesa: chi arriva da adesso in poi non entra (vedi InFilaAsync), quindi
        // aspettare vuol dire aspettare SOLO chi era già dentro — un'attesa che finisce.
        _chiuso = true;
        if (await _tornello.WaitAsync(AttesaMassimaDiChiusura).ConfigureAwait(false))
            _tornello.Release();
        else
            _log.LogWarning(
                "Chiusura editor {Famiglia}: un'operazione era ancora in volo dopo {Secondi}s (documento {DocId}). Lo scope si chiude lo stesso.",
                _famiglia, AttesaMassimaDiChiusura.TotalSeconds, DocumentId);
    }
}
