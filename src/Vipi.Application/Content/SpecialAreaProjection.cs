namespace Vipi.Application.Content;

/// <summary>
/// Proiezione delle aree regolamentate per il viewer: dai dettagli grezzi (metadati + shape JSON) alle viste con
/// poligono proiettato, nell'ordine degli id richiesti. Condivisa da vIPI ACC (per blocco) e vIPI APP non remotizzata.
/// </summary>
public static class SpecialAreaProjection
{
    /// <summary>Viste nell'ordine di <paramref name="orderedIds"/>; gli id senza dettaglio sono saltati.</summary>
    /// <param name="traduci">Come rendere i testi dell'anagrafica nella lingua di chi legge. null =
    /// identità, cioè il comportamento di prima del 28 agosto 2026.
    /// <para>⚠️ Descrizione e dettagli di attivazione li scrive la SORGENTE, in inglese: senza questo, un
    /// lettore italiano trova il documento tradotto e le aree regolamentate ancora in inglese.</para></param>
    public static IReadOnlyList<AccSpecialAreaView> Build(
        IReadOnlyList<SpecialAreaDetail> details, IReadOnlyList<string> orderedIds,
        Func<string?, string?>? traduci = null)
    {
        if (orderedIds.Count == 0) return Array.Empty<AccSpecialAreaView>();
        var byId = new Dictionary<string, SpecialAreaDetail>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in details) byId[d.IvaoId] = d;

        var result = new List<AccSpecialAreaView>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in orderedIds)
        {
            if (!seen.Add(id) || !byId.TryGetValue(id, out var d)) continue;
            var shape = string.IsNullOrWhiteSpace(d.RegionMapPolygon) ? null : Aor.AorPolygonProjector.Project(d.RegionMapPolygon);
            // ⚠️ Il NOME dell'area non si traduce: «LI-R59 Capo Frasca» è un identificatore, e tradurlo
            // renderebbe irriconoscibile la stessa area fra la carta e il documento.
            result.Add(new AccSpecialAreaView(
                d.IvaoId, d.Name, d.Type,
                traduci is null ? d.Description : traduci(d.Description),
                traduci is null ? d.ActivationDetails : traduci(d.ActivationDetails),
                d.MinimumAlt, d.MaximumAlt, shape));
        }
        return result;
    }
}
