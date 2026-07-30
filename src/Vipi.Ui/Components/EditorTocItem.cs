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
public readonly record struct EditorTocItem(
    string AnchorId,
    string Label,
    bool Dirty = false,
    int Level = 2,
    string? GroupLabel = null);
