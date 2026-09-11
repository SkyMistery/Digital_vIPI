using System.Collections.Concurrent;

namespace Vipi.Ui.Tests;

/// <summary>
/// Un dispatcher a <b>thread unico</b>, come quello di Blazor: tutto ciò che gli si posta gira in fila sullo
/// stesso thread, e chi fa <c>await</c> senza <c>ConfigureAwait(false)</c> ci torna sopra.
///
/// <para>🔴 <b>Perché esiste.</b> Sul banco dei test non c'è nessun <see cref="SynchronizationContext"/>: ogni
/// seguito finisce sul pool, con o senza <c>ConfigureAwait(false)</c>, quindi un test «normale» non distingue
/// il codice che torna sul dispatcher da quello che lo abbandona. È esattamente la differenza che in
/// produzione ha dato le NRE di render dell'editor APP (§CW): un caricamento ripartito sul pool scriveva lo
/// stato del componente <b>mentre</b> il dispatcher lo stava disegnando.</para>
/// </summary>
internal sealed class DispatcherDiProva : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Cosa, object? Stato)> _coda = new();
    private readonly Thread _thread;

    public DispatcherDiProva()
    {
        _thread = new Thread(() =>
        {
            SetSynchronizationContext(this);
            foreach (var (cosa, stato) in _coda.GetConsumingEnumerable()) cosa(stato);
        }) { IsBackground = true, Name = "dispatcher di prova" };
        _thread.Start();
    }

    /// <summary>L'id del thread del dispatcher: chi gira qui sopra lo vede come proprio.</summary>
    public int ThreadId => _thread.ManagedThreadId;

    /// <summary>
    /// ⚠️ <b>Dopo la chiusura si posta ancora</b>, ed è normale: il badge «Salvato» del guscio si spegne da
    /// solo due secondi dopo, e quel seguito ha catturato questo contesto. Un <c>Add</c> su una coda chiusa
    /// solleva su un thread del pool, cioè <b>abbatte l'host dei test</b> — e con lui spariscono centinaia
    /// di casi senza un solo rosso (visto l'11 settembre 2026: 1456 casi diventati 233). Quel che arriva
    /// tardi va sul pool: nessuno lo aspetta più, ma non deve far cadere niente.
    /// </summary>
    public override void Post(SendOrPostCallback d, object? state)
    {
        if (!_coda.IsAddingCompleted)
        {
            try { _coda.Add((d, state)); return; }
            catch (InvalidOperationException) { /* chiusa nel frattempo: sotto */ }
        }
        ThreadPool.QueueUserWorkItem(_ => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state) =>
        throw new NotSupportedException("Il dispatcher di prova non esegue in modo sincrono: come Blazor, si posta.");

    /// <summary>Fa partire <paramref name="lavoro"/> sul dispatcher e restituisce il suo <see cref="Task"/>.</summary>
    public Task Esegui(Func<Task> lavoro)
    {
        var fatto = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(async _ =>
        {
            try { await lavoro(); fatto.SetResult(); }
            catch (Exception ex) { fatto.SetException(ex); }
        }, null);
        return fatto.Task;
    }

    public void Dispose() => _coda.CompleteAdding();
}
