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
/// <param name="Level">Livello di indentazione (2 = primo livello, 3 = sotto-voce).</param>
/// <param name="GroupLabel">Intestazione di gruppo opzionale (es. nome blocco ACC); voci con lo stesso
/// gruppo consecutivo condividono un'unica intestazione.</param>
/// <param name="SectionId">Id della sezione rappresentata, quando la voce ne è una: è ciò che rende la voce
/// TRASCINABILE (se l'host ha passato <c>OnReorder</c>). Null per le voci che non sono sezioni — il pannello
/// Release, o il blocco ACC senza figli.</param>
/// <param name="DragGroup">Gruppo di riordino: si trascina solo DENTRO il proprio gruppo (le sezioni di un
/// blocco della vIPI ACC, le radici di una APP o di una vLOA). Voci di gruppi diversi non si accettano.</param>
public readonly record struct EditorTocItem(
    string AnchorId,
    string Label,
    bool Dirty = false,
    int Level = 2,
    string? GroupLabel = null,
    int? SectionId = null,
    string? DragGroup = null);

/// <summary>
/// Esito di un trascinamento nel menu-sezioni: «sposta <paramref name="SectionId"/> prima di
/// <paramref name="BeforeSectionId"/>» (null = in coda al gruppo). È già la forma che vuole
/// <c>IEditingService.MoveSectionBeforeAsync</c>: il conto dei posti lo fa
/// <c>SectionOrdering.TryDropOnto</c>, l'host non lo rifà.
/// </summary>
public readonly record struct TocReorder(int SectionId, int? BeforeSectionId);

/// <summary>
/// Da un albero di sezioni alle voci del menu-sezioni. Funzione <b>pura</b>, come
/// <c>SectionOrdering.TryDropOnto</c>: è la parte del pannello che si può sbagliare senza che nulla protesti,
/// e montare l'editor intero per provarla costerebbe una fixture con servizio di editing e JS.
///
/// <para>⚠️ Fin qui il menu elencava le sole RADICI. Su un documento a un livello solo è la stessa cosa; il
/// vSOP militare ha <b>venti sezioni su ventisei</b> annidate, e chi doveva scrivere le Radioassistenze
/// scorreva ventisei card per trovarle.</para>
///
/// <para>⚠️ Le figlie <b>non si trascinano</b> (<see cref="EditorTocItem.SectionId"/> nullo): il riordino
/// lavora per gruppo di fratelli, e aprirlo ai figli è un lavoro suo. Mezzo lavoro qui darebbe una voce che
/// si lascia prendere e poi non va da nessuna parte — il difetto già pagato con la voce del pannello
/// Release. Il pallino delle modifiche non salvate invece vale anche per loro: è dove si sta scrivendo.</para>
/// </summary>
public static class EditorTocProjection
{
    /// <summary>Livello di indentazione delle sotto-sezioni. Il modello ne consente tre, l'indice ne disegna
    /// due: da qui in giù si resta a 3, invece di rientrare all'infinito in una colonna larga 200px.</summary>
    private const int LivelloFiglie = 3;

    /// <param name="titolo">Come si chiama una sezione a schermo. Null = quel che porta il documento.
    /// <para>⚠️ Serve perché il titolo di una sezione di CATALOGO sta scritto nel documento nella lingua che
    /// aveva alla nascita, e nessuno lo aggiorna quando la lingua cambia: senza questo, l'indice di un vSOP
    /// dichiarato inglese dice «Dati generali» accanto a card intitolate «General data».</para></param>
    public static IReadOnlyList<EditorTocItem> DaSezioni(
        IEnumerable<EditableSection> radici, Func<EditableSection, bool>? dirty, string dragGroup,
        Func<EditableSection, string>? titolo = null)
    {
        var items = new List<EditorTocItem>();
        foreach (var s in radici)
        {
            items.Add(new EditorTocItem($"s-{s.Id}", titolo?.Invoke(s) ?? s.Title, dirty?.Invoke(s) == true,
                SectionId: s.Id, DragGroup: dragGroup));
            Figlie(items, s, dirty, titolo);
        }
        return items;
    }

    private static void Figlie(List<EditorTocItem> items, EditableSection padre, Func<EditableSection, bool>? dirty,
                               Func<EditableSection, string>? titolo)
    {
        foreach (var c in padre.Children)
        {
            items.Add(new EditorTocItem($"s-{c.Id}", titolo?.Invoke(c) ?? c.Title, dirty?.Invoke(c) == true,
                Level: LivelloFiglie));
            Figlie(items, c, dirty, titolo);
        }
    }
}
