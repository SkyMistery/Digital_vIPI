namespace Vipi.Application.Aor;

/// <summary>
/// Regole pure (senza IO) dell'albero di copertura gerarchico: detection ACC estero, anti-ciclo e adiacenza
/// dei settori esteri confinanti. Isolate da <c>EfHierarchyEditingService</c> per essere unit-testabili
/// (doc refactor 06 §4.2). Statiche come <see cref="PolygonGeometry"/>.
/// </summary>
public static class HierarchyRules
{
    /// <summary>
    /// Un ACC è ESTERO se il suo codice non inizia con un prefisso ICAO della divisione (es. "LI"). Basato sui
    /// prefissi, non sul flag <c>Acc.IsForeign</c> (che può essere stale per gli ACC creati da seed demo).
    /// </summary>
    public static bool IsForeignCode(string code, IReadOnlyList<string> divisionPrefixes) =>
        !divisionPrefixes.Any(p => code.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Rifiuta (con <see cref="ValidationException"/>) se il figlio è già (transitivamente) antenato del padre
    /// proposto: risalendo la catena dei padri da <paramref name="proposedParent"/> non deve comparire il figlio.
    /// </summary>
    public static void EnsureNoCycle(string childCallsign, string proposedParent, IReadOnlyDictionary<string, string?> parents)
    {
        var current = (string?)proposedParent;
        var guard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (current is not null && guard.Add(current))
        {
            if (string.Equals(current, childCallsign, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Gerarchia non valida: creerebbe un ciclo.");
            parents.TryGetValue(current, out current);
        }
    }

    /// <summary>
    /// Callsign dei settori esteri che confinano geometricamente (entro <paramref name="thresholdNm"/>) con almeno
    /// un settore domestico. Puro: opera sui poligoni grezzi già forniti.
    /// </summary>
    public static IReadOnlySet<string> ComputeConfiningForeignCallsigns(
        IReadOnlyList<string> domesticPolygons,
        IReadOnlyList<(string Callsign, string? Polygon)> foreignSectors,
        double thresholdNm)
    {
        var domesticRings = domesticPolygons.Select(PolygonGeometry.ToRing).Where(r => r is not null).ToList();
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in foreignSectors)
        {
            var fRing = PolygonGeometry.ToRing(f.Polygon);
            if (fRing is null) continue;
            if (domesticRings.Any(d => PolygonGeometry.AreAdjacent(d, fRing, thresholdNm)))
                result.Add(f.Callsign);
        }
        return result;
    }
}
