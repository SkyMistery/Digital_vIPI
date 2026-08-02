namespace Vipi.Application.Content;

/// <summary>
/// Proiezione delle aree regolamentate per il viewer: dai dettagli grezzi (metadati + shape JSON) alle viste con
/// poligono proiettato, nell'ordine degli id richiesti. Condivisa da vIPI ACC (per blocco) e vIPI APP non remotizzata.
/// </summary>
public static class SpecialAreaProjection
{
    /// <summary>Viste nell'ordine di <paramref name="orderedIds"/>; gli id senza dettaglio sono saltati.</summary>
    public static IReadOnlyList<AccSpecialAreaView> Build(
        IReadOnlyList<SpecialAreaDetail> details, IReadOnlyList<string> orderedIds)
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
            result.Add(new AccSpecialAreaView(d.IvaoId, d.Name, d.Type, d.Description, d.ActivationDetails,
                d.MinimumAlt, d.MaximumAlt, shape));
        }
        return result;
    }
}
