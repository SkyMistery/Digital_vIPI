using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Archi e cerchi dei testi AIP ridotti a vertici (carta <c>docs/feature/2026-09-18-f1-archi-convertitore.md</c>,
/// slice 2). ⚠️ Le tolleranze si scrivono con <c>InRange</c>: <c>Assert.Equal(a, b, 0)</c> arrotonda, non
/// tollera.
/// </summary>
public class ArcGeometryTests
{
    private static readonly (double Lat, double Lon) Centro = (44.0, 8.0);

    [Fact]
    public void Un_Arco_Di_90_Gradi_A_Un_Punto_Per_Grado_Ha_91_Punti_E_Gli_Estremi_Esatti()
    {
        var inizio = ArcGeometry.Destinazione(Centro, 0, 10);
        var fine = ArcGeometry.Destinazione(Centro, 90, 10);

        var arco = ArcGeometry.Arco(inizio, fine, Centro, orario: true, raggioDichiaratoNm: 10);

        Assert.Equal(91, arco.Punti.Count);
        Assert.Equal(inizio, arco.Punti[0]);        // identici, non «vicini»: sono i vertici dei lati accanto
        Assert.Equal(fine, arco.Punti[^1]);
        Assert.All(arco.Punti, p => Assert.InRange(ArcGeometry.DistanzaNm(Centro, p), 9.9999, 10.0001));
        Assert.InRange(ArcGeometry.RottaGradi(Centro, arco.Punti[45]), 44.999, 45.001);
        Assert.False(arco.RaggioIncoerente);
    }

    /// <summary>Stessi estremi, verso opposto: l'altro arco, quello da 270°, che passa da sud-ovest.</summary>
    [Fact]
    public void In_Senso_Antiorario_Si_Fa_L_Altro_Giro()
    {
        var inizio = ArcGeometry.Destinazione(Centro, 0, 10);
        var fine = ArcGeometry.Destinazione(Centro, 90, 10);

        var arco = ArcGeometry.Arco(inizio, fine, Centro, orario: false, raggioDichiaratoNm: 10);

        Assert.Equal(271, arco.Punti.Count);
        Assert.InRange(ArcGeometry.RottaGradi(Centro, arco.Punti[135]), 224.999, 225.001);
    }

    /// <summary>
    /// 🔴 Il raggio si INTERPOLA fra le distanze vere degli estremi: l'arco li tocca tutti e due, e a metà
    /// strada sta a metà raggio. Col raggio dichiarato ci sarebbe uno scalino a un estremo.
    /// </summary>
    [Fact]
    public void Il_Raggio_Si_Interpola_Fra_Gli_Estremi()
    {
        var inizio = ArcGeometry.Destinazione(Centro, 0, 10);
        var fine = ArcGeometry.Destinazione(Centro, 90, 11);

        var arco = ArcGeometry.Arco(inizio, fine, Centro, orario: true, raggioDichiaratoNm: 10.5);

        Assert.Equal(fine, arco.Punti[^1]);
        Assert.InRange(ArcGeometry.DistanzaNm(Centro, arco.Punti[45]), 10.4999, 10.5001);
    }

    /// <summary>
    /// LI R38 (AIP ENR 5.1.2), l'arco col massimo scarto fra i 64 misurati: 91 m sull'ellissoide, 97 sulla
    /// sfera. Deve passare senza avviso; lo stesso arco col centro spostato di 1 NM, no.
    /// </summary>
    [Fact]
    public void LI_R38_Non_Avvisa_E_Un_Centro_Spostato_Di_Un_Miglio_Si()
    {
        var inizio = (36.5, 14 + 47 / 60.0 + 27 / 3600.0);             // 36°30'00"N 014°47'27"E
        var centro = (36 + 40 / 60.0 + 20 / 3600.0, 15 + 53 / 3600.0); // 36°40'20"N 015°00'53"E
        var fine = (36.5, 15 + 14 / 60.0 + 19 / 3600.0);               // 36°30'00"N 015°14'19"E

        var vero = ArcGeometry.Arco(inizio, fine, centro, orario: true, raggioDichiaratoNm: 15);
        Assert.False(vero.RaggioIncoerente);
        Assert.InRange(vero.ScartoNm, 0.01, ArcGeometry.SogliaIncoerenzaNm);

        var spostato = ArcGeometry.Destinazione(centro, 0, 1);
        var storto = ArcGeometry.Arco(inizio, fine, spostato, orario: true, raggioDichiaratoNm: 15);
        Assert.True(storto.RaggioIncoerente);
    }

    [Fact]
    public void Se_Inizio_E_Fine_Coincidono_L_Arco_E_Il_Giro_Intero()
    {
        var p = ArcGeometry.Destinazione(Centro, 30, 5);

        var arco = ArcGeometry.Arco(p, p, Centro, orario: true, raggioDichiaratoNm: 5);

        Assert.Equal(361, arco.Punti.Count);
        Assert.InRange(ArcGeometry.RottaGradi(Centro, arco.Punti[180]), 209.999, 210.001);
    }

    [Theory]
    [InlineData(1, 91)]
    [InlineData(2, 181)]
    [InlineData(100, 901)]      // il tetto: 10 pt/°
    [InlineData(0, 10)]         // il pavimento: un punto ogni 10°
    [InlineData(double.NaN, 91)]
    public void La_Densita_Resta_Fra_Pavimento_E_Tetto(double densita, int attesi)
    {
        var inizio = ArcGeometry.Destinazione(Centro, 0, 10);
        var fine = ArcGeometry.Destinazione(Centro, 90, 10);

        var arco = ArcGeometry.Arco(inizio, fine, Centro, orario: true, raggioDichiaratoNm: 10, puntiPerGrado: densita);

        Assert.Equal(attesi, arco.Punti.Count);
    }

    [Fact]
    public void Il_Cerchio_E_Un_Anello_Da_Nord_In_Senso_Orario_Senza_Ripetere_Il_Primo()
    {
        var cerchio = ArcGeometry.Cerchio(Centro, 1.0);

        Assert.Equal(360, cerchio.Count);
        Assert.All(cerchio, p => Assert.InRange(ArcGeometry.DistanzaNm(Centro, p), 0.99999, 1.00001));
        Assert.True(cerchio[0].Lat > Centro.Lat);                      // nord
        Assert.True(cerchio[90].Lon > Centro.Lon);                     // poi est: orario
        Assert.NotEqual(cerchio[0], cerchio[^1]);
    }

    /// <summary>Le formule contro un valore noto: un grado di latitudine sono 60 NM sulla sfera, a meno del raggio.</summary>
    [Fact]
    public void Un_Grado_Di_Latitudine_E_Circa_60_Miglia()
    {
        Assert.InRange(ArcGeometry.DistanzaNm((44, 8), (45, 8)), 60.02, 60.05);
        Assert.InRange(ArcGeometry.RottaGradi((44, 8), (45, 8)), 0, 1e-9);
        var d = ArcGeometry.Destinazione((44, 8), 90, 30);
        Assert.InRange(ArcGeometry.RottaGradi(d, (44, 8)), 269, 271);
    }
}
