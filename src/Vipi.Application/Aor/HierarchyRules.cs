using static Vipi.Application.Messaggio;
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
    /// Rifiuta (con <see cref="ValidationException"/>) se, nell'albero <paramref name="effectiveParents"/>,
    /// risalire da <paramref name="callsign"/> ci riporta sopra: cioè se il nodo è antenato di sé stesso.
    ///
    /// <para>⚠️ <b>La mappa dev'essere quella dei padri EFFETTIVI</b> (scritto se c'è, altrimenti quello
    /// derivato dalla scaletta d'aeroporto), e dev'essere calcolata <b>dopo</b> aver applicato in memoria la
    /// modifica che si sta validando. Con i soli padri scritti questa guardia ha lasciato passare, in
    /// produzione, <c>LIMF_WW0_APP → LIMF_WN0_APP → LIMF_WW0_APP</c>: <c>WW0</c> non aveva padre scritto,
    /// quindi la catena finiva subito e il ciclo — che esisteva nell'albero che leggono tutti — era
    /// invisibile. Carta <c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §1.</para>
    /// </summary>
    public static void EnsureNoCycle(string callsign, IReadOnlyDictionary<string, string?> effectiveParents)
    {
        if (FindCycleThrough(callsign, effectiveParents) is not { } anello) return;

        var percorso = string.Join(" → ", anello) + " → " + anello[0];
        throw new ValidationException(Lingua(
            $"Gerarchia non valida: creerebbe un ciclo ({percorso}).",
            $"Invalid hierarchy: it would create a cycle ({percorso})."));
    }

    /// <summary>
    /// L'anello che passa per <paramref name="callsign"/> risalendo i padri, o <c>null</c> se non ce n'è.
    /// L'elenco parte dal nodo stesso e non ripete la chiusura (<c>[A, B]</c> significa <c>A → B → A</c>).
    /// </summary>
    public static IReadOnlyList<string>? FindCycleThrough(string callsign, IReadOnlyDictionary<string, string?> parents)
    {
        var percorso = new List<string>();
        var indice = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var corrente = (string?)callsign;

        while (corrente is not null)
        {
            if (indice.TryGetValue(corrente, out var da))
                return percorso.Skip(da).ToList();
            indice[corrente] = percorso.Count;
            percorso.Add(corrente);
            corrente = parents.TryGetValue(corrente, out var p) ? p : null;
        }
        return null;
    }

    /// <summary>
    /// Tutti gli anelli distinti dell'albero dei padri effettivi, uno per anello (non uno per nodo che ci
    /// arriva). Serve al report di consistenza, che deve dire <b>quanti</b> sono e <b>quali</b>, non quante
    /// catene ci finiscono dentro.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> FindAllCycles(IReadOnlyDictionary<string, string?> parents)
    {
        var anelli = new List<IReadOnlyList<string>>();
        var giaInUnAnello = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nodo in parents.Keys)
        {
            if (giaInUnAnello.Contains(nodo)) continue;
            if (FindCycleThrough(nodo, parents) is not { } anello) continue;
            if (anello.Any(giaInUnAnello.Contains)) continue;

            anelli.Add(anello);
            foreach (var n in anello) giaInUnAnello.Add(n);
        }
        return anelli;
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
