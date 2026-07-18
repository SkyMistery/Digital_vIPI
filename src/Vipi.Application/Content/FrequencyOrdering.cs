namespace Vipi.Application.Content;

/// <summary>
/// Ordinamento condiviso delle righe frequenza per l'override per-callsign (dai tasti/drag).
/// Le righe con override vanno in testa nell'ordine indicato; quelle senza restano in coda
/// nell'ordine originale (chiave <c>1000 + indice</c>, <c>OrderBy</c> stabile). Dizionario vuoto
/// = ordine invariato. Usato da <see cref="AccDerivationService"/> e <see cref="AppDocumentService"/>.
/// </summary>
public static class FrequencyOrdering
{
    public static List<AppFreqRow> ApplyOrder(IEnumerable<AppFreqRow> rows, IReadOnlyDictionary<string, int> order) =>
        rows.Select((r, i) => (r, key: order.TryGetValue(r.Callsign, out var ov) ? ov : 1000 + i))
            .OrderBy(x => x.key)
            .Select(x => x.r)
            .ToList();
}
