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

    /// <summary>
    /// Programmarne una nuova annulla la precedente: è quel che faceva il contatore, senza contatore.
    ///
    /// <para>🔴 <b>Questo test cadeva a intermittenza, e la colpa era sua.</b> Fino al 3 settembre 2026
    /// programmava due azioni a 40 ms e poi aspettava <c>Task.Delay(250)</c> <b>fisso</b>: comprava tempo di
    /// orologio a credito, e sotto il carico della suite intera quel credito non c'era.</para>
    ///
    /// <para>⚠️ <b>Misurato invece che indovinato</b> (era la regola scritta in <c>FEATURE-PROCESS</c>:
    /// finché non si sa QUALE delle due strade, cambiare il test è indovinare). Trecento giri sotto zavorra
    /// artificiale del thread pool, due volte: <c>eseguite == 0</c> in <b>109 e 106 giri su 300</b> — la
    /// seconda azione non era ancora partita quando scadevano i 250 ms. <c>eseguite == 2</c> <b>non è mai
    /// comparso in 600 giri</b>: la cancellazione di <see cref="DelayedUiAction"/> è deterministica, e non
    /// era lei il difetto.</para>
    ///
    /// <para>La cura è aspettare il <b>segnale</b> della seconda invece di un tempo, e darle un ritardo
    /// <b>più lungo</b> di quello della prima: quando il segnale arriva, la scadenza della prima è passata da
    /// mezzo secondo, quindi se non fosse stata annullata <c>eseguite</c> sarebbe 2. Stessa zavorra, forma
    /// nuova: <b>600 giri su 600 verdi</b>.</para>
    ///
    /// <para>⚠️ <b>Resta un punto cieco, ed è inerente</b>: provare che una cosa <i>non</i> è successa
    /// vuole un'attesa, e sotto fame di thread nessuna attesa basta sempre. Con la cancellazione rotta di
    /// proposito (mutazione: seconda azione su un'altra istanza) questo test la prende in 219-290 giri su
    /// 300 sotto quella zavorra estrema — non in tutti. La forma vecchia aveva lo <b>stesso</b> punto cieco
    /// <i>e in più</i> i rossi falsi: questa è meglio, non perfetta.</para>
    /// </summary>
    [Fact]
    public async Task La_nuova_annulla_la_precedente()
    {
        using var azione = new DelayedUiAction();
        var eseguite = 0;
        var seconda = new TaskCompletionSource();

        azione.Schedule(TimeSpan.FromMilliseconds(40), () => { Interlocked.Increment(ref eseguite); return Task.CompletedTask; });
        azione.Schedule(TimeSpan.FromMilliseconds(500), () =>
        {
            Interlocked.Increment(ref eseguite);
            seconda.TrySetResult();
            return Task.CompletedTask;
        });

        // ⚠️ Il SEGNALE, non un tempo. Il timeout qui è una rete di sicurezza generosa — come in
        // `Esegue_dopo_il_ritardo` — e non la cosa che si sta misurando.
        await seconda.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
