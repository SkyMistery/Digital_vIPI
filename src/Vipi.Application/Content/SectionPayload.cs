using System.Text.Json;

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
/// La regola vera è <b>«il primo blocco di STRUTTURA»</b>. Un blocco di prosa non ha <c>BodyJson</c> e non si
/// confonde; ma una <b>tabella scritta a mano</b>, un'<b>immagine</b> e un <b>allegato</b> un <c>BodyJson</c>
/// ce l'hanno — quindi «ce l'ha» non basta, e chi sono lo dice <see cref="EEditoriale"/>.
/// </para>
/// <para>
/// ⚠️ <b>Perché non basta: verifica live del 5 settembre 2026.</b> Nel vSOP militare di Grottaglie una tabella
/// scritta a mano in «Radioassistenze» è diventata il payload della scheda — quella sezione nasce senza blocco
/// segnaposto, quindi la tabella era il primo blocco con un JSON. Al primo salvataggio della scheda
/// (una radioassistenza aggiunta) il payload le è stato scritto <b>sopra</b>: contenuto perso, non nascosto, e
/// il blocco sparito anche dall'editor perché il JSON riscritto porta una <c>variant</c>. Chi aggiunge una
/// tabella sopra la scheda non vede un errore — vede la tabella sparire.
/// </para>
/// <para>
/// Il gemello in scrittura sta in <c>EfEditingRepository.SaveSectionBlockJsonAsync</c>, e deve restare la
/// stessa domanda: leggere con una regola e scrivere con un'altra è il modo di perdere un payload — o il
/// contenuto di qualcun altro — senza che nessuno protesti.
/// </para>
/// </summary>
public static class SectionPayload
{
    /// <summary>Il JSON di struttura della sezione, o null se non ce n'è.</summary>
    public static string? Read(SectionView? section) => Read(section?.Blocks);

    /// <summary>Il JSON di struttura fra questi blocchi, nel loro ordine, o null se nessuno ne ha.</summary>
    public static string? Read(IReadOnlyList<BlockView>? blocks) =>
        Scegli(blocks?.Select(b => b.BodyJson));

    /// <summary>Come sopra sui blocchi dell'EDITOR (albero di lavoro), che è la stessa domanda.</summary>
    public static string? Read(IReadOnlyList<EditableBlock>? blocks) =>
        Scegli(blocks?.Select(b => b.BodyJson));

    /// <summary>Il primo JSON di struttura di una sequenza già ordinata, o null.</summary>
    public static string? Scegli(IEnumerable<string?>? jsons)
    {
        if (jsons is null) return null;
        foreach (var j in jsons)
            if (!string.IsNullOrWhiteSpace(j) && !EEditoriale(j)) return j;
        return null;
    }

    /// <summary>
    /// Vero se questo JSON è di un blocco <b>editoriale</b> — quel che scrivono gli editor di blocco: tabella
    /// generica, immagine, allegato. Un blocco così è contenuto di chi redige e non va MAI letto come payload
    /// di una scheda, né riscritto quando la scheda salva.
    /// <para>
    /// Si riconosce dalla FORMA, e non dal formato del blocco, perché il formato non basta: il payload di una
    /// sezione militare è anch'esso un blocco <c>Table</c>. Le tre forme le scrivono
    /// <see cref="MediaRef"/> (<c>mediaId</c>), <see cref="AttachmentRef"/> (<c>ref</c>) e la tabella generica
    /// (<c>columns</c>); un payload che porta <c>columns</c> porta anche una <c>variant</c>, che lo distingue.
    /// </para>
    /// </summary>
    public static bool EEditoriale(string? bodyJson)
    {
        if (string.IsNullOrWhiteSpace(bodyJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(bodyJson);
            // ⚠️ Radice non-oggetto: è la forma storica delle aree regolamentate (`["1029",…]`), che è un
            // payload. E `TryGetProperty` su una radice così alza `InvalidOperationException`, non `JsonException`.
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            var root = doc.RootElement;
            if (root.TryGetProperty("mediaId", out _)) return true;
            if (root.TryGetProperty("ref", out _)) return true;
            return root.TryGetProperty("columns", out _) && !root.TryGetProperty("variant", out _);
        }
        catch (JsonException)
        {
            // JSON rotto: non è una struttura leggibile, e trattarlo come payload vorrebbe dire riscriverlo.
            return true;
        }
    }

    /// <summary>
    /// Come sopra, sui blocchi <b>grezzi</b> di uno snapshot. ⚠️ Serve alla cattura Frozen, che lavora sul
    /// documento com'è in archivio e non sulla vista: è la stessa domanda, e farla in due modi vorrebbe dire
    /// che un giorno la release congela un payload diverso da quello che il viewer legge.
    /// </summary>
    public static string? Read(IReadOnlyList<RawBlock>? blocks) =>
        Scegli(blocks?.Select(b => b.BodyJson));
}
