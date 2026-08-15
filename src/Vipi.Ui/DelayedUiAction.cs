namespace Vipi.Ui;

/// <summary>
/// «Fra N secondi, se nel frattempo non è cambiato niente, fai questo» — per i badge che si spengono da soli
/// («Salvato», «Re-import fatto»). Un'istanza per badge, nel componente che lo mostra.
///
/// <para><b>Perché esiste.</b> Nei quattro editor c'era la stessa identica funzione, copiata:</para>
/// <code>
/// private async Task DismissSavedAsync(int tick)
/// {
///     await Task.Delay(2000);
///     if (tick == _saveTick &amp;&amp; _save == SaveState.Saved) { _save = SaveState.Idle; await InvokeAsync(StateHasChanged); }
/// }
/// </code>
/// <para>chiamata come <c>_ = DismissSavedAsync(tick)</c>. Il contatore <c>tick</c> proteggeva dal salvataggio
/// <i>successivo</i>, non dalla <b>navigazione</b>: chi salvava e cambiava pagina entro due secondi lasciava
/// dietro un <c>InvokeAsync</c> su un renderer smontato, che lancia dentro un task che nessuno osserva.</para>
///
/// <para><b>Cosa cambia.</b> Il contatore diventa un <see cref="CancellationTokenSource"/>: programmare una
/// nuova azione annulla la precedente (stesso effetto del tick, senza il contatore da tenere allineato) e
/// <see cref="Dispose"/> annulla tutto — che è la parte che mancava. Il componente lo chiama nel proprio
/// teardown.</para>
///
/// <para>⚠️ Non è un timer condiviso: ogni badge ha il proprio, perché «Salvato» e «Import fatto» hanno
/// tempi diversi e si spengono in modo indipendente.</para>
/// </summary>
public sealed class DelayedUiAction : IDisposable
{
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Esegue <paramref name="azione"/> dopo <paramref name="ritardo"/>, annullando quella eventualmente già
    /// programmata. Non attende: torna subito. Dopo <see cref="Dispose"/> non programma più nulla.
    /// </summary>
    public void Schedule(TimeSpan ritardo, Func<Task> azione)
    {
        if (_disposed) return;

        var precedente = Interlocked.Exchange(ref _cts, null);
        precedente?.Cancel();
        precedente?.Dispose();

        var cts = new CancellationTokenSource();
        _cts = cts;

        _ = EseguiAsync(ritardo, azione, cts);
    }

    private static async Task EseguiAsync(TimeSpan ritardo, Func<Task> azione, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(ritardo, cts.Token);
            await azione();
        }
        // Annullata (nuova azione programmata, o componente smontato): è l'esito normale, non un errore.
        catch (OperationCanceledException) { }
        // Il renderer non c'è più fra il controllo e la chiamata. La finestra è stretta ma esiste, e qui
        // siamo in un task fire-and-forget: un'eccezione non osservata è esattamente ciò che si vuole evitare.
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        _disposed = true;
        var cts = Interlocked.Exchange(ref _cts, null);
        try { cts?.Cancel(); } catch (ObjectDisposedException) { /* già fermata */ }
        cts?.Dispose();
    }
}
