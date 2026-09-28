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
/// <para>⚠️ <b>Il datum si risolve con <see cref="ShapePart.QuoteAmsl"/></b>, la stessa regola dell'attribuzione del
/// traffico: <c>AGL</c> si alza dell'elevazione dello scalo del settore (<see cref="SectorShape.ElevazioneFt"/>;
/// U-217, revisione totale 3). Il terreno fuori dal campo non ce l'abbiamo, e un settore d'area senza scalo resta
/// com'era. Il testo della fonte resta visibile altrove (<c>BaseRaw</c>), quindi chi legge vede
/// <c>1500 FT AGL</c> anche dove il numero è stato normalizzato.</para>
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
            var (basso, alto) = p.QuoteAmsl(shape.ElevazioneFt);                           // U-217: AGL sul campo
            var (bottom, top) = AorFlBand.ForSource(shape.Source, basso, alto);             // T-046: l'AIP è in piedi
            // `Ref` = la chiave del volume: lega il poligono alla sua riga nella tabella «spazi aerei», che lo
            // accende e lo spegne da sola (carta 2026-09-17-tabella-spazi-aerei-nell-aor.md §6).
            poligoni.Add(proiettato with { LowerFl = bottom, UpperFl = top, Ref = p.SourceRef });
        }

        if (poligoni.Count == 0) return Projected.Empty;

        return new Projected(poligoni, poligoni.Min(p => p.LowerFl), poligoni.Max(p => p.UpperFl));
    }
}
