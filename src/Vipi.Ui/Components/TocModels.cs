using Vipi.Application.Content;

namespace Vipi.Ui.Components;

/// <summary>
/// Una voce del sommario: quel che si legge, dove atterra, e le sue figlie.
///
/// <para>⚠️ <b>Il sommario non conosce più <see cref="SectionView"/>.</b> Fino al 6 settembre 2026 lo
/// conosceva, e la vIPI ACC — le cui sezioni sono <see cref="AccBlockSection"/> dentro dei <b>blocchi</b> —
/// non poteva passare di lì: aveva un indice <b>tutto suo</b>, scritto nella pagina, che si fermava al primo
/// livello. Il risultato si vedeva: una sotto-sezione della ACC («Nuova sezione», misurata dal vivo) non
/// compariva da nessuna parte. Con una voce che è solo <i>titolo + ancora + figlie</i>, ogni famiglia mappa
/// il proprio modello e il sommario è <b>uno solo</b>.</para>
///
/// <para>⚠️ L'<b>ancora</b> è una stringa e non un id di sezione, ed è voluto: la vIPI ACC ancora le sezioni
/// di catalogo che non stanno nel documento su <c>p-{blocco}-{chiave}</c>, non su <c>s-{id}</c>.</para>
/// </summary>
/// <param name="Titolo">Quel che si legge nella voce.</param>
/// <param name="Ancora">L'id su cui atterra, <b>senza</b> il cancelletto.</param>
/// <param name="Figlie">Le sotto-voci. Vuote = la voce è un link e basta, senza involucro.</param>
public sealed record TocVoce(string Titolo, string Ancora, IReadOnlyList<TocVoce> Figlie)
{
    public static TocVoce Foglia(string titolo, string ancora) =>
        new(titolo, ancora, Array.Empty<TocVoce>());

    /// <summary>
    /// Una voce da una sezione del documento, con le sue figlie. È la mappatura che usano quattro famiglie
    /// su cinque.
    /// </summary>
    /// <param name="bozza">Anteprima BOZZA: le sezioni nascoste compaiono. ⚠️ Il default dei chiamanti è
    /// <c>false</c>, quello prudente — chi lo dimentica nasconde, non pubblica per sbaglio.</param>
    /// <param name="ancoraDi">L'ancora del corpo di quella famiglia (default <see cref="SectionView.Id"/>).
    /// ⚠️ Deve restare uguale a quella che usa il corpo, o la voce punta a un id che non esiste e
    /// <b>non fa niente, senza errori</b>.</param>
    /// <param name="figlieDi">La sezione da cui prendere le FIGLIE, quando non è quella stessa: serve a chi
    /// certe figlie le disegna da sé nel corpo. La vLOA rende le due direzioni dei coordinamenti come
    /// intestazioni sue, e quelle <b>non hanno un id</b> su cui atterrare.</param>
    public static IReadOnlyList<TocVoce> Da(
        IReadOnlyList<SectionView> sezioni, bool bozza,
        Func<SectionView, string>? ancoraDi = null,
        Func<SectionView, SectionView>? figlieDi = null) =>
        sezioni
            .Where(s => bozza || !s.IsHidden)
            .Select(s => new TocVoce(
                s.Title,
                ancoraDi?.Invoke(s) ?? s.Id,
                Da((figlieDi?.Invoke(s) ?? s).Children, bozza, ancoraDi, figlieDi)))
            .ToList();
}

/// <summary>
/// Un gruppo di voci con un'intestazione facoltativa. Un documento normale è <b>un</b> gruppo senza titolo;
/// la vIPI ACC ne ha uno per <b>blocco</b>; una pagina unita ne ha uno per <b>documento</b>.
///
/// <para>⚠️ I gruppi stanno <b>dentro lo stesso</b> <c>&lt;aside&gt;</c>, e non in tanti riquadri impilati
/// come faceva <c>UnionToc</c> fino al 6 settembre 2026. Non è estetica: <c>position:sticky</c> si appende
/// al <b>genitore</b>, e impilare due indici obbligava ad avvolgerli in un <c>&lt;div&gt;</c> alto quanto
/// loro — che è esattamente la ragione per cui su tre documenti su cinque il sommario <b>se ne andava in
/// cima scorrendo</b>. Misurato il 6 settembre 2026: genitore alto 392, 359 e 890 px, cioè <b>identico</b>
/// alla barra.</para>
/// </summary>
/// <param name="Titolo">L'intestazione del gruppo, o <c>null</c> per il caso normale (un gruppo solo).</param>
/// <param name="Voci">Le voci del gruppo.</param>
public sealed record TocGruppo(string? Titolo, IReadOnlyList<TocVoce> Voci)
{
    /// <summary>Il caso normale: un gruppo solo, senza intestazione, dalle sezioni del documento.</summary>
    public static IReadOnlyList<TocGruppo> Uno(
        IReadOnlyList<SectionView> sezioni, bool bozza,
        Func<SectionView, string>? ancoraDi = null,
        Func<SectionView, SectionView>? figlieDi = null) =>
        new[] { new TocGruppo(null, TocVoce.Da(sezioni, bozza, ancoraDi, figlieDi)) };
}
