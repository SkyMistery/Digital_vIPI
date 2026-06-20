namespace Vipi.Application.Aor;

/// <summary>
/// Vista in-memory, pura e DB-agnostica, della topologia di una FIR: anagrafica + gerarchia + ownership + regole.
/// Identificatori per callsign/sectorKey così da restare testabile senza EF. SPEC_Logica_AoR §2-3.
/// </summary>
public sealed class Topology
{
    /// <summary>Settori posseduti di default da ogni posizione (callsign → sectorKey[]).</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultSectors { get; init; }

    /// <summary>Genitore top-down di ogni posizione (childCallsign → parentCallsign). Radici assenti.</summary>
    public required IReadOnlyDictionary<string, string> Parent { get; init; }

    /// <summary>Regole di unificazione ordinabili per Priority.</summary>
    public required IReadOnlyList<UnificationRuleSpec> Rules { get; init; }

    /// <summary>Posizioni nel dominio top-down di P (P + chiusura transitiva dei figli).</summary>
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

    /// <summary>Catena di antenati di una posizione fino alla radice (esclusa la posizione stessa).</summary>
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
    IReadOnlyDictionary<string, string> Assignment);       // sectorKey → ownerCallsign
