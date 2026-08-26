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
    /// Le sezioni all'ordine standard (e quelle libere, e tutte se il profilo è null — l'aeroporto non ha
    /// catalogo) non compaiono nel risultato.
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
}
