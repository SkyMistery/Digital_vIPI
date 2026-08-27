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

    /// <param name="famiglia">Come si chiama questo documento nei log: «ACC», «APP», «vLOA», «aeroporto».</param>
    /// <param name="chiaveNoPermesso">Chiave di traduzione del «non hai il permesso» di questa famiglia: è la
    /// sola frase che le quattro non condividono, perché nomina il tipo di documento.</param>
    /// <param name="ridisegna">Come la pagina si fa ridisegnare: <c>() =&gt; InvokeAsync(StateHasChanged)</c>.</param>
    public DocumentEditorShell(IEditingService editing, IJSRuntime js, IStringLocalizer<SharedResource> l,
        ILogger log, string famiglia, string chiaveNoPermesso, Func<Task> ridisegna)
    {
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
            if (DocumentId is int id) Lock = await _editing.InspectLockAsync(id);
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

    /// <summary>Esce dalla modifica e molla il lock, così un altro editore può entrare senza aspettarne la scadenza.</summary>
    public async Task FinishEditingAsync()
    {
        if (DocumentId is not int id) return;
        await GuardCoreAsync(() => _editing.ReleaseLockAsync(id), silenziosa: true);
        Lock = await _editing.InspectLockAsync(id);
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

    /// <summary>Ferma il badge che si spegne da solo: dopo lo smontaggio non c'è più un renderer da avvisare.</summary>
    public void Dispose() => _spegniSalvato.Dispose();
}
