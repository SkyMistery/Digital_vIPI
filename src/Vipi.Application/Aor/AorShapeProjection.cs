using Vipi.Application.Airspace;
using Vipi.Application.Content;

namespace Vipi.Application.Aor;

/// <summary>
/// Da <see cref="SectorShape"/> — quel che dice la porta unica — a quel che la mappa disegna: i poligoni
/// proiettati, <b>ognuno con la sua banda</b>, più l'inviluppo del settore.
///
/// <para>⚠️ <b>L'inviluppo serve alla legenda e all'ordinamento, non al disegno.</b> Su <c>LIBA_APP</c>, che
/// è <c>GND → FL105</c> su una zona e <c>7000 FT AMSL → FL195</c> sull'altra, l'inviluppo è
/// <c>GND → FL195</c>: esattamente il monoblocco generoso dell'anagrafica. Estrudere quello vorrebbe dire
/// disegnare un parallelepipedo unico dove il cielo vero ha due gradini — ed è il difetto che questa carta
/// chiude (<c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c>).</para>
///
/// <para>⚠️ <b>Il datum si risolve qui</b>, e in nessun altro posto: <c>AGL</c> si tratta come <c>AMSL</c>,
/// perché il terreno non ce l'abbiamo. Il testo della fonte resta visibile altrove (<c>BaseRaw</c>), quindi
/// chi legge vede <c>7000 FT AMSL</c> anche dove il numero è stato normalizzato.</para>
///
/// PURA: nessun I/O, deterministica, testabile da sola.
/// </summary>
public static class AorShapeProjection
{
    /// <summary>I poligoni con la loro banda, e l'inviluppo (base più bassa, tetto più alto).</summary>
    public sealed record Projected(IReadOnlyList<AppAorPolygon> Polygons, int? LowerFl, int? UpperFl)
    {
        public static Projected Empty { get; } = new(Array.Empty<AppAorPolygon>(), null, null);
        public bool IsEmpty => Polygons.Count == 0;
    }

    public static Projected Project(SectorShape? shape)
    {
        if (shape is null || shape.Parts.Count == 0) return Projected.Empty;

        var poligoni = new List<AppAorPolygon>(shape.Parts.Count);
        foreach (var p in shape.Parts)
        {
            var proiettato = AorPolygonProjector.Project(p.PolygonJson);
            if (proiettato is null) continue;   // ⚠️ un anello rotto non porta via gli altri sei
            var (bottom, top) = AorFlBand.Normalize(p.BaseFeet, p.TopFeet);
            poligoni.Add(proiettato with { LowerFl = bottom, UpperFl = top });
        }

        if (poligoni.Count == 0) return Projected.Empty;

        return new Projected(poligoni, poligoni.Min(p => p.LowerFl), poligoni.Max(p => p.UpperFl));
    }
}
