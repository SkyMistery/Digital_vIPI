using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Dove vive il testo comune di una sezione (S127, <see cref="TestoComune"/>): una riga di <c>SharedBlocks</c>, come le
/// intro di pagina. Nessuna tabella nuova.
/// </summary>
public interface ITestoComuneStore
{
    /// <summary>
    /// I blocchi del testo comune di una sezione, nell'ordine in cui si mostrano. Nessun testo → lista vuota.
    /// <para>È una <b>lettura pubblica</b>: la chiede la vista di ogni documento con quella sezione.</para>
    /// </summary>
    Task<IReadOnlyList<ExtraBlock>> LeggiAsync(string sezione, CancellationToken ct = default);

    /// <summary>
    /// Sostituisce il testo comune di una sezione. Elenco vuoto = il testo comune non c'è più.
    ///
    /// <para>⚠️ <b>Solo gli admin</b>, e il cancello è qui e non nella pagina: un salvataggio cambia il testo di tutti i
    /// documenti con quella sezione insieme. E col lock (<see cref="TestoComune.ChiaveLock"/>) tenuto da chi salva: la
    /// riga si riscrive per intero, e chi l'ha perso coprirebbe il lavoro di chi ce l'ha adesso.</para>
    /// </summary>
    Task SalvaAsync(string sezione, IReadOnlyList<ExtraBlock> blocchi, CancellationToken ct = default);
}

/// <summary>Il testo comune come si rende: lo chiedono la vista derivata e la cattura della release.</summary>
public static class TestoComuneStoreExtensions
{
    public static async Task<IReadOnlyList<BlockView>> VistaAsync(this ITestoComuneStore store, string sezione,
        CancellationToken ct = default) =>
        TestoComune.Vista(await store.LeggiAsync(sezione, ct));
}
