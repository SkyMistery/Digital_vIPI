using Vipi.Application.Abstractions;
using Vipi.Application.Aor;

namespace Vipi.Application.Content;

/// <summary>
/// Quali settori di due ACC confinano: per <b>geometria</b>, dai poligoni di confine di oggi (CTR/FSS del catalogo).
///
/// <para>🔴 U-158 (revisione totale 3): le definizioni erano tre. La vLOA derivava con la geometria, chi cercava i
/// documenti da avvisare leggeva l'elenco fermo all'ultimo import dei confinanti, e la coppia della vLOA portava
/// un terzo elenco (catalogo intero come ripiego) che nessuno leggeva. Una regola sola, qui; l'elenco del candidato
/// resta solo per chi cerca, perché un settore sparito non ha più il poligono.</para>
/// </summary>
public static class VloaConfinanti
{
    public static (List<string> Home, List<string> Foreign) Calcola(
        IEnumerable<VloaSectorPoly> home, IEnumerable<VloaSectorPoly> foreign, double sogliaNm)
    {
        var homeRings = home.Select(p => (p.Callsign, Ring: PolygonGeometry.ToRing(p.Raw)))
            .Where(x => x.Ring is not null).ToList();
        var foreignRings = foreign.Select(p => (p.Callsign, Ring: PolygonGeometry.ToRing(p.Raw)))
            .Where(x => x.Ring is not null).ToList();

        var h = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var f = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in homeRings)
            foreach (var b in foreignRings)
                if (PolygonGeometry.AreAdjacent(a.Ring, b.Ring, sogliaNm))
                {
                    h.Add(a.Callsign);
                    f.Add(b.Callsign);
                }
        return (h.ToList(), f.ToList());
    }
}
