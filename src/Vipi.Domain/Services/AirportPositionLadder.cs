namespace Vipi.Domain.Services;

/// <summary>Una posizione d'aeroporto vista dalla scaletta: il minimo che serve per calcolare il padre.</summary>
public readonly record struct LadderPosition(string Callsign, SectorType Type, string? ParentCallsign);

/// <summary>
/// Padre di copertura di una posizione d'aeroporto quando il catalogo non ne porta uno esplicito.
///
/// Il legame che l'admin compila in <c>/services/vsop/admin/sector-structure</c> sta sul nodo AEROPORTO
/// (<c>Airport.ParentCallsign</c>) e vale per tutte le sue posizioni; le posizioni salgono verso di esso lungo
/// la scaletta operativa <b>DEL → GND → TWR → APP</b>.
///
/// Puro e deterministico: lo condividono la proiezione dei settori (che scrive <c>Sector.ParentSectorId</c>) e
/// l'editor della gerarchia (che deve mostrare lo stesso padre come «ereditato», altrimenti le due schermate
/// direbbero cose diverse sullo stesso nodo).
/// </summary>
public static class AirportPositionLadder
{
    /// <summary>Posto nella scaletta: più basso = più in alto nella gerarchia. Fuori da un aeroporto (CTR/FSS) = 0.</summary>
    public static int Rung(SectorType type) => type switch
    {
        SectorType.App => 5,
        SectorType.Twr or SectorType.ITwr => 10,
        SectorType.Gnd => 20,
        SectorType.Del => 30,
        _ => 0,
    };

    /// <summary>
    /// Padre ereditato di <paramref name="position"/>. Sale i gradini sopra di sé fermandosi al primo che dà UNA
    /// risposta sola (<see cref="PickOnRung"/>); esaurita la scaletta esce su <paramref name="airportParent"/>.
    /// Null = nessun padre derivabile (l'aeroporto non ha un padre configurato).
    /// </summary>
    public static string? ParentOf(
        LadderPosition position,
        IReadOnlyList<LadderPosition> airportPositions,
        string? airportParent,
        string icao)
    {
        var mine = Rung(position.Type);
        var siblings = airportPositions
            .Where(p => !string.Equals(p.Callsign, position.Callsign, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Gradini sopra di me, dal più vicino al più lontano: se uno è ambiguo si sale, invece di tirare a sorte.
        foreach (var rung in siblings.Select(p => Rung(p.Type)).Where(o => o < mine).Distinct().OrderByDescending(o => o))
        {
            var pick = PickOnRung(siblings.Where(p => Rung(p.Type) == rung).ToList(), icao);
            if (pick is not null) return pick;
        }

        return airportParent;
    }

    /// <summary>
    /// La posizione di riferimento fra quelle di pari grado. Null = gradino ambiguo (il chiamante sale).
    /// <list type="number">
    /// <item>Una sola candidata: è quella.</item>
    /// <item><b>Radice del sottoalbero</b>: se fra le candidate una gerarchia è già scritta — è il caso degli APP,
    /// che nell'editor struttura sono nodi con un padre — vale quella dell'admin. La radice è l'unica il cui padre
    /// sta fuori dal gruppo (su LIRF le sei APP pendono da <c>LIRF_TW1_APP</c>).</item>
    /// <item><b>Callsign senza infisso</b> (<c>LIRF_TWR</c> vs <c>LIRF_E_TWR</c>): convenzione di divisione per la
    /// posizione principale.</item>
    /// </list>
    /// </summary>
    public static string? PickOnRung(IReadOnlyList<LadderPosition> candidates, string icao)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0].Callsign;

        var names = candidates.Select(c => c.Callsign).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roots = candidates.Where(c => c.ParentCallsign is null || !names.Contains(c.ParentCallsign)).ToList();
        if (roots.Count == 1) return roots[0].Callsign;

        var pool = roots.Count > 1 ? roots : candidates;
        var plain = pool.Where(c => IsPlainCallsign(c.Callsign, icao)).ToList();
        return plain.Count == 1 ? plain[0].Callsign : null;
    }

    /// <summary>Callsign a due soli pezzi, <c>{ICAO}_{TIPO}</c>: la posizione principale, non uno split
    /// (<c>LIRF_TWR</c> sì, <c>LIRF_E_TWR</c> no).</summary>
    private static bool IsPlainCallsign(string callsign, string icao) =>
        callsign.StartsWith(icao + "_", StringComparison.OrdinalIgnoreCase)
        && callsign.Count(ch => ch == '_') == 1;
}
