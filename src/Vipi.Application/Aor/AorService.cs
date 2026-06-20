using Vipi.Domain;

namespace Vipi.Application.Aor;

/// <summary>Risultato della risoluzione AoR: ownership e stato di ogni settore del dominio di P.</summary>
public sealed class AorResult
{
    /// <summary>sectorKey → callsign che lo possiede, data la configurazione online.</summary>
    public required IReadOnlyDictionary<string, string> Ownership { get; init; }

    /// <summary>sectorKey → stato (Covered = lo copro io P, Online = lo gestisce un subordinato online).</summary>
    public required IReadOnlyDictionary<string, SectorState> State { get; init; }
}

/// <summary>
/// Risolve l'ownership e lo stato dei settori (la parte più critica del sistema). ADR-0001 D5.
/// Puro: nessun I/O, deterministico, cacheable. Sorgente dei test = SPEC_Logica_AoR §5 (S1–S10).
/// </summary>
public interface IAorService
{
    /// <summary>Calcola ownership e stato dei settori nel dominio di <paramref name="p"/> dato l'insieme online.</summary>
    AorResult Resolve(Topology topology, string p, IReadOnlySet<string> online);
}

/// <inheritdoc cref="IAorService"/>
public sealed class AorService : IAorService
{
    public AorResult Resolve(Topology topology, string p, IReadOnlySet<string> online)
    {
        var domain = topology.DomainOf(p);

        // Settori del dominio = unione dei settori di default delle posizioni in Dom(P).
        var sectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pos in domain)
            if (topology.DefaultSectors.TryGetValue(pos, out var owned))
                foreach (var s in owned) sectors.Add(s);

        // 1. Ownership di default: ogni settore appartiene alla posizione che lo possiede.
        var ownership = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pos in domain)
            if (topology.DefaultSectors.TryGetValue(pos, out var owned))
                foreach (var s in owned) ownership[s] = pos;

        // 2. Applica le regole di unificazione (per Priority) la cui condizione è soddisfatta da O.
        foreach (var rule in topology.Rules.OrderBy(r => r.Priority))
        {
            if (rule.RequiredOnline.All(online.Contains))
                foreach (var (sector, owner) in rule.Assignment)
                    if (sectors.Contains(sector)) ownership[sector] = owner;
        }

        // 3. Top-down: se l'owner non è online, ricade sul primo antenato online in Dom(P), altrimenti su P.
        foreach (var s in sectors)
        {
            var owner = ownership[s];
            if (!online.Contains(owner))
                ownership[s] = NearestOnlineAncestor(topology, owner, domain, online, p);
        }

        // 4. Stato: Online se gestito da un subordinato online diverso da P, altrimenti Covered.
        var state = new Dictionary<string, SectorState>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sectors)
        {
            var owner = ownership[s];
            state[s] = online.Contains(owner) && !owner.Equals(p, StringComparison.OrdinalIgnoreCase)
                ? SectorState.Online
                : SectorState.Covered;
        }

        return new AorResult { Ownership = ownership, State = state };
    }

    private static string NearestOnlineAncestor(
        Topology topology, string owner, IReadOnlySet<string> domain,
        IReadOnlySet<string> online, string p)
    {
        foreach (var ancestor in topology.Ancestors(owner))
        {
            if (!domain.Contains(ancestor)) break;          // uscito dal dominio di P
            if (ancestor.Equals(p, StringComparison.OrdinalIgnoreCase)) return p;
            if (online.Contains(ancestor)) return ancestor;
        }
        return p; // nessun antenato online: lo copre P (top-down completo)
    }
}
