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
