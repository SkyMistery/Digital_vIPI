using Vipi.Application.Content;

namespace Vipi.Ui.Components;

/// <summary>
/// Voce del menu-sezioni laterale (TOC) degli editor. Proiezione di una sezione già in memoria:
/// <paramref name="AnchorId"/> è l'id dell'elemento su cui saltare (es. <c>s-42</c>, <c>sec-freq</c>,
/// <c>blk-7</c>, <c>p-release</c>). Non tocca il DB. Vedi docs/feature/2026-07-29-toc-editor.md.
/// </summary>
/// <param name="AnchorId">Id dell'elemento target (senza '#').</param>
/// <param name="Label">Etichetta mostrata nel TOC.</param>
/// <param name="Dirty">Se la sezione ha modifiche non salvate (mostra un pallino).</param>
/// <param name="GroupLabel">Intestazione di gruppo opzionale (es. nome blocco ACC); voci con lo stesso
/// gruppo consecutivo condividono un'unica intestazione.</param>
/// <param name="SectionId">Id della sezione rappresentata, quando la voce ne è una: è ciò che rende la voce
/// TRASCINABILE (se l'host ha passato <c>OnReorder</c>). Null per le voci che non sono sezioni — il pannello
/// Release, o il blocco ACC senza figli.</param>
/// <param name="DragGroup">L'ALBERO di riordino: si trascina solo dentro il proprio albero (le sezioni di un
/// blocco della vIPI ACC, le sezioni di un membro in un documento unito). Voci di alberi diversi non si
/// accettano — ⚠️ e questo è ciò che impedisce a una sezione di cambiare documento.</param>
/// <param name="ParentSectionId">Il padre della sezione dentro l'albero mostrato (null = è una radice di
/// quell'albero). Due voci sono FRATELLI se condividono albero e padre: è così che il pannello distingue un
/// riordino da uno spostamento di gruppo (carta 2026-09-04).</param>
/// <param name="Movable">La sezione può cambiare gruppo: solo le LIBERE. Una di catalogo si riordina fra i
/// suoi fratelli ma non cambia padre — glielo assegna il catalogo.</param>
/// <param name="SectionDepth">Profondità della sezione nel documento: serve a sapere se, lasciandola su una
/// voce, il sottoalbero che si porta dietro ci starebbe.</param>
/// <param name="SubtreeHeight">Quanti livelli scende il sottoalbero della sezione (0 = nessuna figlia).</param>
public readonly record struct EditorTocItem(
    string AnchorId,
    string Label,
    bool Dirty = false,
    string? GroupLabel = null,
    int? SectionId = null,
    string? DragGroup = null,
    int? ParentSectionId = null,
    bool Movable = false,
    int SectionDepth = 0,
    int SubtreeHeight = 0);

/// <summary>
/// Esito di un trascinamento nel menu-sezioni. Due gesti in uno, e li distingue <paramref name="CambiaPadre"/>:
/// <list type="bullet">
/// <item>riordino fra FRATELLI — «sposta <paramref name="SectionId"/> prima di
/// <paramref name="BeforeSectionId"/>» (null = in coda), la forma che vuole
/// <c>IEditingService.MoveSectionBeforeAsync</c>;</item>
/// <item>cambio di GRUPPO — la sezione prende il posto di quella su cui è stata lasciata, quindi diventa
/// figlia di <paramref name="NuovoPadreId"/> e si infila prima di lei
/// (<c>IEditingService.MoveSectionToParentAsync</c>).</item>
/// </list>
/// Il conto dei posti lo fa <c>SectionOrdering.TryDropOnto</c>, l'host non lo rifà.
/// </summary>
public readonly record struct TocReorder(
    int SectionId, int? BeforeSectionId, bool CambiaPadre = false, int? NuovoPadreId = null);

/// <summary>
/// Da un albero di sezioni alle voci del menu-sezioni. Funzione <b>pura</b>, come
/// <c>SectionOrdering.TryDropOnto</c>: è la parte del pannello che si può sbagliare senza che nulla protesti,
/// e montare l'editor intero per provarla costerebbe una fixture con servizio di editing e JS.
///
/// <para>⚠️ Fin qui il menu elencava le sole RADICI. Su un documento a un livello solo è la stessa cosa; il
/// vSOP militare ha <b>venti sezioni su ventisei</b> annidate, e chi doveva scrivere le Radioassistenze
/// scorreva ventisei card per trovarle.</para>
///
/// <para>⚠️ E fino al 4 settembre 2026 le figlie <b>non si trascinavano</b> (<see cref="EditorTocItem.SectionId"/>
/// nullo): il riordino lavorava per gruppo di fratelli, e aprirlo ai figli era «un lavoro suo». È questo. Ora
/// ogni voce porta il proprio padre, se è libera e quanto è alto il suo sottoalbero — cioè quel che serve al
/// pannello per distinguere un riordino da un cambio di gruppo e per non offrire un gesto che il motore
/// rifiuterebbe.</para>
/// </summary>
public static class EditorTocProjection
{
    /// <param name="radici">Le sezioni di primo livello dell'albero mostrato.</param>
    /// <param name="dirty">Quali sezioni hanno modifiche non salvate.</param>
    /// <param name="dragGroup">L'albero: due voci con questo valore diverso non si accettano mai.</param>
    /// <param name="titolo">Come si chiama una sezione a schermo. Null = quel che porta il documento.
    /// <para>⚠️ Serve perché il titolo di una sezione di CATALOGO sta scritto nel documento nella lingua che
    /// aveva alla nascita, e nessuno lo aggiorna quando la lingua cambia: senza questo, l'indice di un vSOP
    /// dichiarato inglese dice «Dati generali» accanto a card intitolate «General data».</para></param>
    /// <param name="radiceId">Il padre delle radici mostrate (null = la radice del documento; per la vIPI ACC
    /// sarebbe l'Id della sezione-blocco). È il <c>ParentSectionId</c> delle voci di primo livello.</param>
    public static IReadOnlyList<EditorTocItem> DaSezioni(
        IEnumerable<EditableSection> radici, Func<EditableSection, bool>? dirty, string dragGroup,
        Func<EditableSection, string>? titolo = null, int? radiceId = null)
    {
        var items = new List<EditorTocItem>();
        foreach (var s in radici)
        {
            items.Add(Voce(s, titolo, dirty, dragGroup, radiceId));
            Figlie(items, s, dirty, dragGroup, titolo);
        }
        return items;
    }

    private static void Figlie(List<EditorTocItem> items, EditableSection padre, Func<EditableSection, bool>? dirty,
                               string dragGroup, Func<EditableSection, string>? titolo)
    {
        foreach (var c in padre.Children)
        {
            items.Add(Voce(c, titolo, dirty, dragGroup, padre.Id));
            Figlie(items, c, dirty, dragGroup, titolo);
        }
    }

    private static EditorTocItem Voce(EditableSection s, Func<EditableSection, string>? titolo,
        Func<EditableSection, bool>? dirty, string dragGroup, int? padreId) =>
        new($"s-{s.Id}", titolo?.Invoke(s) ?? s.Title, dirty?.Invoke(s) == true,
            SectionId: s.Id, DragGroup: dragGroup, ParentSectionId: padreId,
            Movable: SectionMoveTargets.Spostabile(s), SectionDepth: s.Depth,
            SubtreeHeight: SectionMoveTargets.Altezza(s));

    /// <summary>
    /// Dall'elenco <b>piatto</b> del menu ai <b>gruppi</b> del sommario condiviso. È il passo che rende la
    /// barra degli editor uguale a quella dei documenti, e sta qui — in una funzione <b>pura</b> — per la
    /// stessa ragione di <see cref="DaSezioni"/>: è la parte che si può sbagliare senza che nulla protesti.
    ///
    /// <para>⚠️ L'albero si ricostruisce da <see cref="EditorTocItem.ParentSectionId"/>, <b>non</b> dal
    /// rientro: il rientro è una conseguenza, e ricavarne la gerarchia vorrebbe dire indovinarla. La
    /// proiezione emette il padre <b>subito prima</b> delle sue figlie (visita in profondità), quindi basta
    /// ricordare dove mettere le figlie di ogni sezione già vista.</para>
    ///
    /// <para>⚠️ Una voce il cui padre <b>non è in elenco</b> è una radice: è il caso della vIPI ACC, dove
    /// il padre delle voci di primo livello è la sezione-BLOCCO, che nel menu non compare come voce.</para>
    ///
    /// <para>⚠️ I gruppi si chiudono per etichetta <b>consecutiva</b>: due blocchi omonimi restano due
    /// gruppi e non si fondono per via del nome. E una voce <b>senza</b> etichetta apre un gruppo suo, senza
    /// intestazione — prima finiva sotto l'ultima intestazione emessa, cioè sotto un titolo che non era il
    /// suo (nella vIPI ACC è il caso del pannello Release, in coda a tutti).</para>
    /// </summary>
    public static IReadOnlyList<TocGruppo> Gruppi(IReadOnlyList<EditorTocItem> items)
    {
        var gruppi = new List<TocGruppo>();
        List<TocVoce>? voci = null;
        Dictionary<int, List<TocVoce>> perSezione = new();
        string? etichetta = null;

        foreach (var it in items)
        {
            var sua = it.GroupLabel is { Length: > 0 } g ? g : null;
            if (voci is null || !string.Equals(sua, etichetta, StringComparison.Ordinal))
            {
                if (voci is not null) gruppi.Add(new TocGruppo(etichetta, voci));
                voci = new List<TocVoce>();
                // Il padre di una voce sta sempre nel SUO gruppo: azzerare qui evita che una figlia si
                // appenda a una sezione omonima del gruppo precedente.
                perSezione = new Dictionary<int, List<TocVoce>>();
                etichetta = sua;
            }

            var figlie = new List<TocVoce>();
            var voce = new TocVoce(it.Label, it.AnchorId, figlie);
            if (it.SectionId is int id) perSezione[id] = figlie;

            if (it.ParentSectionId is int padre && perSezione.TryGetValue(padre, out var sorelle)) sorelle.Add(voce);
            else voci.Add(voce);
        }

        if (voci is not null) gruppi.Add(new TocGruppo(etichetta, voci));
        return gruppi;
    }
}
