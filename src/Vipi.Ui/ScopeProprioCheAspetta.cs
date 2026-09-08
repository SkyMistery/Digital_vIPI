using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Vipi.Ui;

/// <summary>
/// Uno scope proprio che, quando il componente se ne va, <b>aspetta chi è ancora dentro</b>.
///
/// <para>🔴 <b>La terza porta.</b> Su un componente interattivo se ne conoscevano due, e servono a due cose
/// diverse: lo <b>scope proprio</b> (<see cref="OwningComponentBase"/>) protegge dagli ALTRI — nessuno usa il
/// tuo <c>DbContext</c> — e la <b>sentinella di rientro</b> protegge da SÉ STESSI — un gesto non parte sopra
/// il precedente. <b>Nessuna delle due protegge dal TEMPO.</b> L'utente cambia pagina, il circuito finisce,
/// Blazor smonta il componente e <c>OwningComponentBase</c> chiude lo scope — e con lui il <c>DbContext</c> e
/// la sua connessione — <b>mentre una query è ancora aperta</b>.</para>
///
/// <para>🔴 <b>Misurato in produzione</b>, non dedotto. Le trentanove <c>ObjectDisposedException</c> del 4-7
/// settembre 2026 erano questo, lette come rumore («l'utente ha cambiato pagina»); e l'8 settembre, con le
/// correzioni di §CD già in linea, <c>StatsDivisionPage</c> — che ha <b>entrambe</b> le altre porte — è
/// caduta lo stesso alle 20:26:02, in <c>EfAtcStatsQueries.ByPositionAsync</c>. Vedi
/// <c>docs/lavori-aperti.md</c> §CF.</para>
///
/// <para>🔴 <b>E il conto lo paga un terzo.</b> Una sessione MySQL restituita al pool con una lettura ancora
/// aperta la prende poi chi la chiede dopo: «<c>This method may not be called when another read operation is
/// pending</c>», «<c>Connection must be Open; current state is Closed</c>»,
/// «<c>Packet received out-of-order</c>» — sei voci in un pomeriggio, su contesti diversi, allo stesso
/// secondo.</para>
///
/// <para>⚠️ <b>Non è una porta nuova: è la stessa di <c>DocumentEditorShell.ChiudiAsync</c></b>, che dal 7
/// settembre 2026 fa esattamente questo per i cinque editor. Quella vive nella shell, che le pagine non
/// hanno; questa sta nella base, dove ce l'hanno tutti. La regola è la stessa e sono le stesse righe:
/// <b>prima la porta, poi l'attesa</b>.</para>
///
/// <para><b>Come si usa</b>: <c>@inherits ScopeProprioCheAspetta</c> al posto di
/// <c>@inherits OwningComponentBase</c>, e i <b>caricamenti</b> passano da
/// <see cref="InFilaAsync"/>. I <b>gesti</b> che scrivono possono passarci o no: la corsa col caricamento non
/// ce l'hanno, ma se ci passano l'attesa copre anche loro.</para>
/// </summary>
public abstract class ScopeProprioCheAspetta : OwningComponentBase, IAsyncDisposable
{
    /// <summary>
    /// ⚠️ <b>Non si smaltisce mai</b>, ed è una correzione già pagata sul tornello dell'editor (4 settembre
    /// 2026, cinque circuiti abbattuti in un'ora): chi lascia la pagina mentre un caricamento è in volo fa
    /// arrivare quel caricamento al suo <c>finally</c> — cioè al <c>Release()</c> — <b>dopo</b> la chiusura.
    /// Su un semaforo smaltito quella <c>Release</c> è una <c>ObjectDisposedException</c> sollevata dalla
    /// continuazione di un <c>Task</c> che nessuno aspetta più: non la cattura nessuno, e Blazor risponde
    /// nel solo modo che conosce, abbattendo il circuito.
    ///
    /// <para>Non smaltirlo non perde niente: <see cref="SemaphoreSlim.Dispose()"/> serve solo a chi ha
    /// chiesto <c>AvailableWaitHandle</c>, che qui non tocca nessuno.</para>
    /// </summary>
    private readonly SemaphoreSlim _porta = new(1, 1);

    /// <summary>
    /// Chi è già dentro non deve rimettersi in fila dietro sé stesso: un caricamento che ne chiama un altro
    /// aspetterebbe un permesso che tiene lui, e resterebbe lì per sempre. <c>AsyncLocal</c> perché la
    /// catena è asincrona: un <c>bool</c> di campo direbbe «dentro» anche a chi entra da un altro gesto.
    ///
    /// <para>⚠️ <b>Di istanza, non statico</b>, come l'omologo del tornello dell'editor. Statico, il
    /// permesso che tiene <b>questo</b> componente varrebbe anche per un <b>altro</b>: un genitore che nel
    /// suo caricamento aspetta quello di un figlio farebbe entrare il figlio <b>senza</b> la sua porta, e la
    /// chiusura del figlio non avrebbe nessuno da aspettare. Il rientro è una faccenda fra sé e sé.</para>
    /// </summary>
    private readonly AsyncLocal<bool> _giaDentro = new();

    /// <summary>Vero dopo <see cref="DisposeAsync"/>: da qui in poi non entra più nessuno.</summary>
    private bool _chiusa;

    /// <summary>
    /// ⚠️ Il tetto esiste perché un'attesa senza fine è il modo silenzioso di tenere in piedi un circuito.
    /// Quindici secondi è la stessa misura del tornello dell'editor, per la stessa ragione: è più di
    /// qualunque query sana e meno di qualunque pazienza umana.
    /// </summary>
    private static readonly TimeSpan AttesaMassimaDiChiusura = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Vero quando il componente si sta smontando. ⚠️ Un caricamento lungo, fatto di più <c>await</c>, può
    /// guardarlo <b>fra un passo e l'altro</b> e smettere: l'attesa qui sotto è un tetto, non un permesso di
    /// prendersela comoda.
    /// </summary>
    protected bool Chiusa => _chiusa;

    /// <summary>
    /// La porta del caricamento: registra che <b>qualcuno è dentro</b>, così la chiusura sa chi aspettare.
    ///
    /// <para>⚠️ <b>Non sostituisce la sentinella di rientro della pagina</b>, e non prova a indovinarla: se
    /// due gesti chiedono lo stesso caricamento, qui il secondo <b>aspetta</b> il primo e poi rifà il giro —
    /// che è corretto ma non sempre è quel che si vuole (una chip premuta tre volte non merita tre letture).
    /// Chi ha una sentinella la tiene: decide lei chi entra, questa decide solo che si esce prima di
    /// chiudere.</para>
    ///
    /// <para>⚠️ Chi arriva a porta chiusa <b>non aspetta</b>: torna indietro subito. Aspettare una porta che
    /// non si riapre è la stessa perdita di memoria di prima, scritta al contrario.</para>
    /// </summary>
    protected async Task InFilaAsync(Func<Task> caricamento)
    {
        if (_chiusa) return;
        if (_giaDentro.Value) { await caricamento(); return; }

        await _porta.WaitAsync().ConfigureAwait(false);
        _giaDentro.Value = true;
        try { await caricamento(); }
        finally
        {
            _giaDentro.Value = false;
            _porta.Release();
        }
    }

    /// <inheritdoc cref="InFilaAsync(Func{Task})"/>
    protected async Task<T?> InFilaAsync<T>(Func<Task<T>> caricamento)
    {
        T? esito = default;
        await InFilaAsync(async () => esito = await caricamento());
        return esito;
    }

    /// <summary>
    /// Il registro, chiesto <b>solo se serve</b> — cioè solo quando l'attesa scade.
    ///
    /// <para>⚠️ <b>Non è un <c>[Inject]</c></b>, ed è una scelta: una proprietà iniettata obbligherebbe ogni
    /// contenitore che disegna uno di questi componenti a registrare <c>ILoggerFactory</c>, banchi di prova
    /// compresi — un componente che si rifiuta di apparire perché non trova il suo <i>logger</i> è un prezzo
    /// alto per una riga che si scrive quasi mai. <c>GetService</c> (non <c>GetRequiredService</c>) risponde
    /// <c>null</c> dove non c'è, e la riga semplicemente non si scrive.</para>
    ///
    /// <para>Lo scope è ancora vivo qui: <see cref="DisposeAsync"/> lo smaltisce <b>dopo</b>.</para>
    /// </summary>
    private ILogger? Registro() =>
        ScopedServices.GetService(typeof(ILoggerFactory)) is ILoggerFactory f ? f.CreateLogger(GetType()) : null;

    /// <summary>
    /// Da sovrascrivere quando c'è qualcosa da fermare <b>prima</b> di aspettare: un badge che si spegne da
    /// solo, un timer, una sottoscrizione. Gira a porta già chiusa.
    /// </summary>
    protected virtual ValueTask PrimaDiChiudereAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Chiude la porta, aspetta chi era dentro, <b>e poi</b> smaltisce lo scope.
    ///
    /// <para>🔴 <b>La riga nel <c>finally</c> è quella che conta.</b> Quando un componente è
    /// <c>IAsyncDisposable</c>, Blazor chiama <b>solo</b> <c>DisposeAsync</c> — mai il <c>Dispose</c>
    /// sincrono, che è quello con cui <see cref="OwningComponentBase"/> chiude il proprio scope. Senza quella
    /// riga ogni visita lascerebbe in piedi uno scope con dentro un <c>DbContext</c>: un guasto che non si
    /// vede il primo giorno e si vede il quindicesimo.</para>
    ///
    /// <para>⚠️ <b>Prima la porta, poi l'attesa.</b> Chi arriva da adesso in poi non entra, quindi aspettare
    /// vuol dire aspettare <b>solo chi era già dentro</b>: un'attesa che finisce.</para>
    ///
    /// <para>⚠️ Scaduto il tetto si chiude lo stesso — è il comportamento di prima, il male minore già noto,
    /// non un modo nuovo di piantarsi. Ma si scrive nel log, perché una chiusura che scade è un caricamento
    /// che non torna, cioè una cosa da guardare.</para>
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_chiusa) return;
            _chiusa = true;

            await PrimaDiChiudereAsync().ConfigureAwait(false);

            if (await _porta.WaitAsync(AttesaMassimaDiChiusura).ConfigureAwait(false))
                _porta.Release();
            else
                Registro()?.LogWarning(
                    "Chiusura di {Componente}: un caricamento era ancora in volo dopo {Secondi}s. Lo scope si chiude lo stesso.",
                    GetType().Name, AttesaMassimaDiChiusura.TotalSeconds);
        }
        finally
        {
            ((IDisposable)this).Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
