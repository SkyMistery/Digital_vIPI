namespace Vipi.Application.Content;

/// <summary>
/// Scostamento dall'ordine STANDARD di catalogo delle sezioni di un gruppo (fratelli con lo stesso padre: le
/// sezioni di un blocco della vIPI ACC, le radici di una vIPI APP o di una vLOA).
/// <para>
/// L'ordine del documento è quello dei <c>DocumentSection.Order</c> — il catalogo lo decide solo alla nascita e
/// quando riconcilia una sezione mancante. Chi scrive può spostare una sezione dentro il suo gruppo; questa
/// classe dice di QUANTI posti se n'è allontanata, così l'editor lo può mostrare accanto al titolo.
/// </para>
/// <para>
/// ⚠️ Si confrontano solo le sezioni FISSE fra loro (quelle che il catalogo conosce, di primo livello o
/// sotto-sezioni fisse via <see cref="SectionCatalog.Find"/>): una sezione libera non ha una posizione standard,
/// e contarla sposterebbe di un posto tutte le fisse che la seguono senza che nessuno le abbia toccate.
/// </para>
/// </summary>
public static class SectionOrdering
{
    /// <summary>
    /// Scostamento per Id sezione: negativo = tanti posti PIÙ IN ALTO dello standard, positivo = più in basso.
    /// Le sezioni all'ordine standard, quelle libere e tutte quelle di un documento senza profilo (null) non
    /// compaiono nel risultato.
    /// </summary>
    public static IReadOnlyDictionary<int, int> OffsetsFromStandard(
        SectionProfile? profile, IReadOnlyList<EditableSection> siblings)
    {
        var empty = new Dictionary<int, int>();
        if (profile is not { } p || siblings.Count == 0) return empty;

        // Sottosuccessione delle sole sezioni di catalogo, nell'ordine ATTUALE del documento.
        var fixedNow = new List<(int Id, int StandardOrder)>();
        foreach (var s in siblings)
            if (SectionCatalog.Find(p, s.SectionKey) is { } d)
                fixedNow.Add((s.Id, d.Order));
        if (fixedNow.Count < 2) return empty;   // con una sola sezione fissa non esiste uno scostamento

        // Ordine standard della STESSA sottosuccessione: chi manca dal documento non lascia un buco.
        // ThenBy sull'indice attuale = ordinamento stabile anche se due chiavi condividessero l'ordine.
        var standard = fixedNow
            .Select((x, i) => (x.Id, x.StandardOrder, Now: i))
            .OrderBy(x => x.StandardOrder).ThenBy(x => x.Now)
            .ToList();

        var offsets = new Dictionary<int, int>();
        for (var std = 0; std < standard.Count; std++)
        {
            var delta = standard[std].Now - std;
            if (delta != 0) offsets[standard[std].Id] = delta;
        }
        return offsets;
    }

    /// <summary>
    /// Traduce un TRASCINAMENTO nel riferimento che vuole il motore: «metti la sezione spostata prima di
    /// questa» (null = in coda). La regola letta a schermo è una sola — <b>la sezione lasciata prende il posto
    /// di quella su cui la si lascia</b> — e da qui esce nei due versi:
    /// verso il basso si inserisce DOPO il bersaglio, verso l'alto PRIMA.
    /// <para>
    /// Vale solo dentro un gruppo: <paramref name="siblingIds"/> sono i fratelli nell'ordine attuale. Se uno
    /// dei due non è del gruppo, o sono lo stesso, non c'è mossa (<c>false</c>).
    /// </para>
    /// </summary>
    /// <param name="siblingIds">Id dei fratelli nell'ordine attuale del documento.</param>
    /// <param name="movedId">Sezione trascinata.</param>
    /// <param name="targetId">Sezione su cui è stata lasciata.</param>
    /// <param name="beforeId">Riferimento per <c>MoveSectionBeforeAsync</c> (null = in coda al gruppo).</param>
    public static bool TryDropOnto(IReadOnlyList<int> siblingIds, int movedId, int targetId, out int? beforeId)
    {
        beforeId = null;
        if (movedId == targetId) return false;

        var from = -1;
        var to = -1;
        for (var i = 0; i < siblingIds.Count; i++)
        {
            if (siblingIds[i] == movedId) from = i;
            if (siblingIds[i] == targetId) to = i;
        }
        if (from < 0 || to < 0) return false;

        // Verso il basso il posto del bersaglio si libera solo passandogli sopra: il riferimento è il fratello
        // SUCCESSIVO al bersaglio (e se non c'è, la coda).
        if (from < to)
        {
            beforeId = to + 1 < siblingIds.Count ? siblingIds[to + 1] : null;
            return true;
        }

        beforeId = targetId;
        return true;
    }
}
