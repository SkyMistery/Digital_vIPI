using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Proiezione poligono AoR (pura, difensiva): forme valide → path SVG; input degenere → null.</summary>
public class AorPolygonProjectorTests
{
    [Fact]
    public void Square_Pairs_LngLat_Produces_Closed_Path()
    {
        // Coppie nel formato IVAO regionMapPolygon: [lng, lat]. Quadrato ~1°×1°.
        var json = "[[10,43],[11,43],[11,44],[10,44]]";
        var poly = AorPolygonProjector.Project(json);

        Assert.NotNull(poly);
        Assert.StartsWith("M", poly!.Path);
        Assert.EndsWith("Z", poly.Path);
        Assert.Equal(4, poly.Path.Count(c => c is 'M' or 'L'));   // 4 vertici
        Assert.StartsWith("0 0 ", poly.ViewBox);
    }

    [Fact]
    public void Real_Pisa_App_Polygon_Projects_With_Correct_Aspect()
    {
        // regionMapPolygon reale di LIRP_APP (coppie [lng, lat], anello chiuso → 10 vertici).
        var json = "[[10.145555,43.264166],[10.239722,43.1275],[10.452222,43.140555]," +
                   "[10.756944,43.808333],[10.572222,43.942777],[9.833333,44.075]," +
                   "[9.787222,43.777777],[9.7,43.623888],[9.981666,43.262222],[10.145555,43.264166]]";
        var poly = AorPolygonProjector.Project(json);

        Assert.NotNull(poly);
        Assert.Equal(10, poly!.Path.Count(c => c is 'M' or 'L'));

        // Estensione: lon span ~1.057° · cos(43.6°)≈0.724 → ~0.765; lat span ~0.947°. Lato lungo = lat → riempie il canvas (~400).
        var parts = poly.ViewBox.Split(' ');
        var w = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
        var h = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(h > w, $"atteso poligono più alto che largo (h={h}, w={w})");   // shape verticale corretta
        Assert.True(h is >= 399 and <= 401, $"il lato lungo riempie il canvas: h={h}");
        Assert.True(w is > 300 and < 360, $"larghezza proiettata attesa ~326: w={w}");
    }

    [Fact]
    public void Objects_With_Lat_Lon_Are_Parsed()
    {
        var json = """[{"lat":43,"lon":10},{"lat":43,"lon":11},{"lat":44,"lon":10.5}]""";
        var poly = AorPolygonProjector.Project(json);
        Assert.NotNull(poly);
        Assert.Equal(3, poly!.Path.Count(c => c is 'M' or 'L'));
    }

    [Fact]
    public void Nested_Ring_Is_Unwrapped()
    {
        var json = "[[[43,10],[43,11],[44,11],[44,10]]]";
        var poly = AorPolygonProjector.Project(json);
        Assert.NotNull(poly);
        Assert.Equal(4, poly!.Path.Count(c => c is 'M' or 'L'));
    }

    [Fact]
    public void Wrapper_Object_Coordinates_Property_Is_Parsed()
    {
        var json = """{"coordinates":[[43,10],[43,11],[44,11]]}""";
        var poly = AorPolygonProjector.Project(json);
        Assert.NotNull(poly);
        Assert.Equal(3, poly!.Path.Count(c => c is 'M' or 'L'));
    }

    /// <summary>
    /// Un punto ripetuto <b>di fila</b> non entra nella proiezione: <see cref="PolygonGeometry.ParsePoints"/>
    /// lo toglie — è un lato a lunghezza zero, veleno per la triangolazione dell'estrusione 3D — e il
    /// proiettore vede l'elenco già ripulito. Quindi il disegno è identico a quello del poligono senza il
    /// doppione, <b>viewBox compreso</b>: il riquadro dipende dalla latitudine <i>media</i>, cioè da quanti
    /// punti ci sono, non solo da dove stanno.
    ///
    /// <para>⚠️ Congelato da un controesempio vero (CsCheck, seed <c>bxKC4K6PiVz6</c>, 25 agosto 2026), come
    /// prescrive il commento di <c>AorProiezioneProperties</c>: la proprietà sul rapporto fra i lati rifaceva
    /// il conto sui punti <b>generati</b> mentre il proiettore lavora su quelli <b>parsati</b>, e i due
    /// <c>cos(lat media)</c> divergevano — 374,0 contro 371,7 di larghezza. Sembrava un rosso intermittente
    /// per due giorni; era un elenco di punti diverso.</para>
    /// </summary>
    [Fact]
    public void Un_Punto_Ripetuto_Di_Fila_Non_Cambia_La_Proiezione()
    {
        // ⚠️ Formato IVAO: coppie [lng, lat], LONGITUDINE PRIMA.
        var conGemello = "[[13,36],[13,36],[10,46],[18,42]]";
        var senzaGemello = "[[13,36],[10,46],[18,42]]";

        var a = AorPolygonProjector.Project(conGemello);
        var b = AorPolygonProjector.Project(senzaGemello);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(b!.ViewBox, a!.ViewBox);
        Assert.Equal(b.Path, a.Path);
        Assert.Equal(3, a.Path.Count(c => c is 'M' or 'L'));   // tre punti disegnati, non quattro
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("[[43,10],[43,11]]")]            // < 3 punti
    [InlineData("[[43,10],[43,10],[43,10]]")]    // tutti coincidenti
    public void Degenerate_Or_Malformed_Returns_Null(string? json)
    {
        Assert.Null(AorPolygonProjector.Project(json));
    }
}
