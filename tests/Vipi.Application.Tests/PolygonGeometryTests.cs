using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Adiacenza geometrica pura: poligoni combacianti → confinanti; lontani → no; soglia rispettata.</summary>
public class PolygonGeometryTests
{
    // Quadrati ~1°×1° in formato IVAO [lng, lat]. A copre lon 10..11 / lat 43..44.
    private const string SquareA = "[[10,43],[11,43],[11,44],[10,44]]";
    // B condivide il bordo est di A (lon=11).
    private const string SquareB_Touching = "[[11,43],[12,43],[12,44],[11,44]]";
    // C è staccato di ~0.1° in longitudine (gap ≈ 4.3 NM alla latitudine 43.5).
    private const string SquareC_SmallGap = "[[11.1,43],[12.1,43],[12.1,44],[11.1,44]]";
    // D lontano (lon 20..21): mai confinante con A.
    private const string SquareD_Far = "[[20,43],[21,43],[21,44],[20,44]]";

    [Fact]
    public void Touching_Squares_Are_Adjacent()
    {
        var a = PolygonGeometry.ToRing(SquareA);
        var b = PolygonGeometry.ToRing(SquareB_Touching);
        Assert.True(PolygonGeometry.AreAdjacent(a, b, thresholdNm: 8.0));
        Assert.True(PolygonGeometry.MinEdgeDistanceNm(a!.Points, b!.Points) < 0.001);
    }

    [Fact]
    public void Far_Squares_Are_Not_Adjacent()
    {
        var a = PolygonGeometry.ToRing(SquareA);
        var d = PolygonGeometry.ToRing(SquareD_Far);
        Assert.False(PolygonGeometry.AreAdjacent(a, d, thresholdNm: 8.0));
    }

    [Fact]
    public void Small_Gap_Respects_Threshold()
    {
        var a = PolygonGeometry.ToRing(SquareA);
        var c = PolygonGeometry.ToRing(SquareC_SmallGap);
        // Gap ~4.3 NM: dentro soglia 8 → confinante; fuori soglia 2 → no.
        Assert.True(PolygonGeometry.AreAdjacent(a, c, thresholdNm: 8.0));
        Assert.False(PolygonGeometry.AreAdjacent(a, c, thresholdNm: 2.0));
    }

    [Fact]
    public void Null_Or_Degenerate_Ring_Is_Not_Adjacent()
    {
        var a = PolygonGeometry.ToRing(SquareA);
        Assert.Null(PolygonGeometry.ToRing("[[10,43],[11,43]]"));   // < 3 punti → null
        Assert.False(PolygonGeometry.AreAdjacent(a, null, 8.0));
        Assert.False(PolygonGeometry.AreAdjacent(null, a, 8.0));
    }

    [Fact]
    public void MinEdgeDistance_Between_Far_Squares_Is_Roughly_Correct()
    {
        var a = PolygonGeometry.ToRing(SquareA);
        var d = PolygonGeometry.ToRing(SquareD_Far);
        // Gap in longitudine da lon=11 a lon=20 = 9° · cos(43.5)≈0.725 · 60 ≈ 391 NM.
        var dist = PolygonGeometry.MinEdgeDistanceNm(a!.Points, d!.Points);
        Assert.True(dist is > 350 and < 430, $"distanza attesa ~391 NM, ottenuta {dist:0}");
    }

    /// <summary>
    /// Un anello incapsulato (<c>[[[lng,lat],…]]</c>) si legge: è la forma con cui la sorgente manda alcuni
    /// poligoni, ed era già gestita.
    /// </summary>
    [Fact]
    public void Un_anello_incapsulato_si_legge()
    {
        var pts = PolygonGeometry.ParsePoints("[" + SquareA + "]");

        Assert.Equal(4, pts.Count);
        Assert.Equal((43.0, 10.0), pts[0]);
    }

    /// <summary>
    /// <b>Fotografa un limite noto, non lo approva.</b> Con più anelli allo stesso livello — un MultiPolygon,
    /// o un poligono con un buco — <c>ExtractPoints</c> scende nel PRIMO e ignora gli altri, in silenzio.
    ///
    /// <para>Misurato l'11 agosto 2026 su <c>vipi.db</c>: <b>zero</b> casi del genere su 1338 poligoni reali
    /// (1273 anelli singoli, 50 colonne vuote, 15 array vuoti). Per questo il comportamento non è stato
    /// cambiato: far restituire più anelli a <c>ToRing</c> vorrebbe dire toccare tutti i consumatori — mappa
    /// AoR, adiacenza dei confinanti, poligoni pubblicati — per un caso che non si verifica.</para>
    ///
    /// <para>⚠️ Se un giorno questo test comincia a sembrare sbagliato, è perché la misura è cambiata: allora
    /// la correzione è restituire tutti gli anelli, non aggiustare l'asserzione. Un settore che perde metà
    /// della propria forma sbaglia i vicini, e i vicini decidono i coordinamenti.</para>
    /// </summary>
    [Fact]
    public void Con_piu_anelli_si_legge_solo_il_primo_ed_e_un_limite_noto()
    {
        var multi = "[" + SquareA + "," + SquareD_Far + "]";

        var pts = PolygonGeometry.ParsePoints(multi);

        Assert.Equal(4, pts.Count);                       // solo il primo anello
        Assert.DoesNotContain(pts, p => p.Lon >= 20.0);   // il secondo (lon 20..21) è sparito
    }
}
