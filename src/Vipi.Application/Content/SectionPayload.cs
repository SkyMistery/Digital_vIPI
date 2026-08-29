namespace Vipi.Application.Content;

/// <summary>
/// Dove sta il <b>payload</b> di una sezione editoriale-strutturata: le configurazioni di un APP, la selezione
/// delle aree, e — dalla carta dei vSOP militari §12 — le tabelle a mano del vSOP militare.
///
/// <para>
/// La convenzione storica è «il <c>BodyJson</c> del <b>primo</b> blocco della sezione», scritta cinque volte in
/// cinque file. Regge finché la sezione ha un blocco solo, e le sezioni derivate dell'APP ne hanno uno solo per
/// costruzione. <b>Non regge sulle sezioni del vSOP militare</b>, che il caricatore dei SOP riempie di prosa:
/// lì il payload convive con i blocchi scritti a mano, e «il primo» diventa «quello che per caso sta in cima
/// oggi». Chi aggiunge un paragrafo sopra la tabella non vede un errore — vede la tabella svuotarsi.
/// </para>
/// <para>
/// La regola vera è <b>«il primo blocco che un payload ce l'ha»</b>: un blocco di prosa non ha
/// <c>BodyJson</c>, quindi i due non si confondono, e sulle sezioni con un blocco solo la risposta è identica
/// a quella di prima. Il gemello in scrittura sta in <c>EfEditingRepository.SaveSectionBlockJsonAsync</c>, e
/// deve restare la stessa domanda: leggere con una regola e scrivere con un'altra è il modo di perdere un
/// payload senza che nessuno protesti.
/// </para>
/// </summary>
public static class SectionPayload
{
    /// <summary>Il JSON di struttura della sezione, o null se non ce n'è.</summary>
    public static string? Read(SectionView? section) => Read(section?.Blocks);

    /// <summary>Il JSON di struttura fra questi blocchi, nel loro ordine, o null se nessuno ne ha.</summary>
    public static string? Read(IReadOnlyList<BlockView>? blocks)
    {
        if (blocks is null) return null;
        foreach (var b in blocks)
            if (!string.IsNullOrWhiteSpace(b.BodyJson)) return b.BodyJson;
        return null;
    }
}
