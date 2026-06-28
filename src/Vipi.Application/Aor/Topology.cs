namespace Vipi.Application.Aor;

/// <summary>
/// Vista in-memory, pura e DB-agnostica, della topologia di una ACC: contenimento (albero) + regole.
/// Settore == posizione: ogni settore è identificato dal proprio callsign e possiede sé stesso di default.
/// SPEC_Logica_AoR §2-3.
/// </summary>
public sealed class Topology
{
    /// <summary>Tutti i callsign dei settori della ACC (radici incluse, anche senza figli).</summary>
    public required IReadOnlyCollection<string> Sectors { get; init; }

    /// <summary>Padre top-down (contenimento) di ogni settore (childCallsign → parentCallsign). Radici assenti.</summary>
    public required IReadOnlyDictionary<string, string> Parent { get; init; }

    /// <summary>Regole di unificazione ordinabili per Priority.</summary>
    public required IReadOnlyList<UnificationRuleSpec> Rules { get; init; }

    /// <summary>Settori nel dominio top-down di P (P + chiusura transitiva dei figli).</summary>
    public IReadOnlySet<string> DomainOf(string p)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { p };
        // figli = posizioni il cui genitore è già nel dominio (chiusura a punto fisso)
        bool added = true;
        while (added)
        {
            added = false;
            foreach (var (child, parent) in Parent)
            {
                if (result.Contains(parent) && result.Add(child))
                    added = true;
            }
        }
        return result;
    }

    /// <summary>Catena di antenati di un settore fino alla radice (escluso il settore stesso).</summary>
    public IEnumerable<string> Ancestors(string callsign)
    {
        var current = callsign;
        var guard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (Parent.TryGetValue(current, out var parent) && guard.Add(parent))
        {
            yield return parent;
            current = parent;
        }
    }
}

/// <summary>Regola di unificazione già deserializzata (la forma JSON vive in Infrastructure). PIANO §20.5.</summary>
public sealed record UnificationRuleSpec(
    string Name,
    int Priority,
    IReadOnlyCollection<string> RequiredOnline,            // condizione: tutti online
    IReadOnlyDictionary<string, string> Assignment);       // settoreCallsign → ownerCallsign
