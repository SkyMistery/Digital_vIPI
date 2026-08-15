using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il badge che si spegne da solo. Sostituisce quattro copie della stessa funzione con un contatore
/// (<c>_saveTick</c>) che proteggeva dal salvataggio <i>successivo</i> ma non dalla <b>navigazione</b>: chi
/// salvava e cambiava pagina entro due secondi lasciava un <c>InvokeAsync</c> su un renderer smontato, dentro
/// un task che nessuno osserva.
/// </summary>
public sealed class DelayedUiActionTests
{
    [Fact]
    public async Task Esegue_dopo_il_ritardo()
    {
        using var azione = new DelayedUiAction();
        var fatto = new TaskCompletionSource();

        azione.Schedule(TimeSpan.FromMilliseconds(20), () => { fatto.TrySetResult(); return Task.CompletedTask; });

        await fatto.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>Programmarne una nuova annulla la precedente: è quel che faceva il contatore, senza contatore.</summary>
    [Fact]
    public async Task La_nuova_annulla_la_precedente()
    {
        using var azione = new DelayedUiAction();
        var eseguite = 0;

        azione.Schedule(TimeSpan.FromMilliseconds(40), () => { Interlocked.Increment(ref eseguite); return Task.CompletedTask; });
        azione.Schedule(TimeSpan.FromMilliseconds(40), () => { Interlocked.Increment(ref eseguite); return Task.CompletedTask; });

        await Task.Delay(250);
        Assert.Equal(1, Volatile.Read(ref eseguite));
    }

    /// <summary>Il punto del cambio: smontato il componente, l'azione non parte più.</summary>
    [Fact]
    public async Task Dispose_annulla_quella_in_volo()
    {
        var azione = new DelayedUiAction();
        var eseguite = 0;

        azione.Schedule(TimeSpan.FromMilliseconds(60), () => { Interlocked.Increment(ref eseguite); return Task.CompletedTask; });
        azione.Dispose();

        await Task.Delay(250);
        Assert.Equal(0, Volatile.Read(ref eseguite));
    }

    /// <summary>Dopo Dispose non si programma più nulla: il teardown può incrociare un'ultima chiamata.</summary>
    [Fact]
    public async Task Dopo_Dispose_non_programma_piu()
    {
        var azione = new DelayedUiAction();
        azione.Dispose();

        var eseguite = 0;
        azione.Schedule(TimeSpan.FromMilliseconds(20), () => { Interlocked.Increment(ref eseguite); return Task.CompletedTask; });

        await Task.Delay(150);
        Assert.Equal(0, Volatile.Read(ref eseguite));
    }

    /// <summary>
    /// Se l'azione lancia <see cref="ObjectDisposedException"/> — il renderer sparito fra il controllo e la
    /// chiamata — non deve restare un'eccezione non osservata: siamo in un task fire-and-forget.
    /// </summary>
    [Fact]
    public async Task Un_renderer_sparito_non_lascia_eccezioni_non_osservate()
    {
        using var azione = new DelayedUiAction();
        var lanciato = new TaskCompletionSource();

        azione.Schedule(TimeSpan.FromMilliseconds(20), () =>
        {
            lanciato.TrySetResult();
            throw new ObjectDisposedException("Renderer");
        });

        await lanciato.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);

        GC.Collect();
        GC.WaitForPendingFinalizers();   // un task fallito e non osservato affiorerebbe qui
    }
}
