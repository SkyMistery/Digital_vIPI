using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I testi dell'AIP incollati nel convertitore: archi, cerchi e frasi (carta
/// <c>docs/feature/2026-09-18-f1-archi-convertitore.md</c>).
/// </summary>
public class CoordinateParserAipTests
{
    /// <summary>L'esempio del committente, 17 settembre 2026 (carta F1 §0).</summary>
    private const string EsempioDelCommittente =
        "44°51'24\" N 008°14'57\" E\n" +
        "then arc of circle in clockwise direction radius 17 NM centred on\n" +
        "44°55'29\" N 007°51'43\" E\n" +
        "till point\n" +
        "44°41'08\" N 008°04'34\" E";

    /// <summary>
    /// 🔴 <b>Era la caratterizzazione del difetto</b> (slice 1): il parser a righe metteva il <b>centro</b> fra i
    /// vertici (l'area usciva di 3 punti, il secondo era 44°55'29"N), il <c>17</c> del raggio era un «angolo
    /// spaiato» alla riga 2 e <c>till point</c> una riga non letta alla 4. <b>Ribaltato di proposito</b> nella
    /// slice 6, quando il lettore AIP è entrato in <see cref="CoordinateParser.Parse"/>: ora il centro non c'è,
    /// le due segnalazioni nemmeno, e fra i due estremi c'è l'arco.
    /// </summary>
    [Fact]
    public void Il_Centro_Dell_Arco_Non_E_Piu_Un_Vertice()
    {
        var esito = CoordinateParser.Parse(EsempioDelCommittente);

        Assert.Empty(esito.Segnalazioni);
        var area = Assert.Single(esito.Aree);
        Assert.True(area.Punti.Count > 10);
        Assert.DoesNotContain(area.Punti, p => Math.Abs(p.Lat - 44.92472222) < 1e-6 && Math.Abs(p.Lon - 7.86194444) < 1e-6);
        Assert.All(area.Punti, p => Assert.InRange(ArcGeometry.DistanzaNm((44.92472222, 7.86194444), p), 16.9, 17.1));
    }

    /// <summary>La densità passa da <see cref="CoordinateParser.Parse"/> al lettore AIP.</summary>
    [Fact]
    public void La_Densita_Arriva_Al_Lettore_AIP()
    {
        var uno = Assert.Single(CoordinateParser.Parse(EsempioDelCommittente).Aree).Punti.Count;
        var tre = Assert.Single(CoordinateParser.Parse(EsempioDelCommittente, puntiPerGrado: 3).Aree).Punti.Count;

        Assert.InRange(tre, 3 * uno - 4, 3 * uno);
    }

    /// <summary>
    /// ⚠️ Il ramo AIP scatta SOLO con una frase lunga. Un sectorfile coi commenti degli AOD («then», «point»)
    /// resta al parser a righe, identico: i nomi dal 6° campo ci sono ancora, ed è il segno del lettore giusto.
    /// </summary>
    [Fact]
    public void Un_Sectorfile_Commentato_Resta_Al_Parser_A_Righe()
    {
        var esito = CoordinateParser.Parse(
            "// then point from here\nN042.00.28.000;E011.58.06.000;N041.59.26.000;E011.59.00.000;RESTRICT;R14A;");

        Assert.Equal("R14A", Assert.Single(esito.Aree).Nome);
    }

    /// <summary>KML e JSON si decidono PRIMA: una descrizione KML che parla di «point of origin» resta KML.</summary>
    [Fact]
    public void Il_KML_Si_Decide_Prima_Del_Testo_AIP()
    {
        var esito = CoordinateParser.Parse(
            "<kml><Placemark><name>R1</name><description>to point of origin</description><Polygon><outerBoundaryIs>" +
            "<LinearRing><coordinates>9,45,0 9.1,45,0 9.1,45.1,0 9,45,0</coordinates></LinearRing></outerBoundaryIs>" +
            "</Polygon></Placemark></kml>");

        Assert.Equal("R1", Assert.Single(esito.Aree).Nome);
    }

    /// <summary>
    /// Il tetto dei punti generati: sei cerchi a 10 pt/° sarebbero 21 600 punti. Il sesto esce rado, e il
    /// tetto si dice UNA volta.
    /// </summary>
    [Fact]
    public void Oltre_Il_Tetto_I_Cerchi_Escono_Radi_E_Si_Dice_Una_Volta()
    {
        var cerchio = "Circular area centered on 45°00'00\"N 009°00'00\"E within a 1.0 NM radius.\n";

        var esito = CoordinateParser.Parse(string.Concat(Enumerable.Repeat(cerchio, 6)), puntiPerGrado: 10);

        Assert.Equal(6, esito.Aree.Count);
        Assert.Equal([3600, 3600, 3600, 3600, 3600, 36], esito.Aree.Select(a => a.Punti.Count));
        var s = Assert.Single(esito.Segnalazioni);
        Assert.Equal(CoordinateIssueKind.TroppiPunti, s.Kind);
    }

    /// <summary>
    /// 🔴 <b>L'emisfero staccato</b>, come lo scrive l'AIP (<c>24" N</c>). Prima diventava un'etichetta: il
    /// primo faceva da tipo, l'ultimo da nome, e l'esempio del committente usciva come un'area di tipo «N» e
    /// nome «E». Trovato scrivendo la caratterizzazione qui sopra.
    /// </summary>
    [Fact]
    public void L_Emisfero_Staccato_Non_Diventa_Un_Nome()
    {
        var esito = CoordinateParser.Parse("44°51'24\" N 008°14'57\" E\n44°41'08\" N 008°04'34\" E");

        var area = Assert.Single(esito.Aree);
        Assert.Null(area.Nome);
        Assert.Null(area.Tipo);
        Assert.Equal(2, area.Punti.Count);
        Assert.Equal(44.85666667, area.Punti[0].Lat, 5);
        Assert.Equal(8.24916667, area.Punti[0].Lon, 5);
        Assert.Empty(esito.Segnalazioni);
    }

    /// <summary>
    /// ⚠️ Il caso che rendeva il difetto pericoloso: un <c>S</c> o un <c>W</c> staccati si perdevano, e il
    /// punto cambiava emisfero senza nessuna segnalazione.
    /// </summary>
    [Theory]
    [InlineData("33°52'00\" S 151°12'00\" E", -33.86666667, 151.2)]
    [InlineData("40°38'00\" N 073°47'00\" W", 40.63333333, -73.78333333)]
    [InlineData("040°38'00\" W 40°38'00\" N", 40.63333333, -40.63333333)]   // l'emisfero decide l'asse
    public void L_Emisfero_Staccato_Decide_Segno_E_Asse(string riga, double lat, double lon)
    {
        var esito = CoordinateParser.Parse(riga);

        var p = Assert.Single(Assert.Single(esito.Aree).Punti);
        Assert.Equal(lat, p.Lat, 5);
        Assert.Equal(lon, p.Lon, 5);
        Assert.Empty(esito.Segnalazioni);
    }

    /// <summary>
    /// La lettera sola si attacca SOLO all'angolo appena letto che non l'aveva detta: dopo un angolo che ha
    /// già il suo emisfero, o lontano da un angolo, resta l'etichetta che era.
    /// </summary>
    [Theory]
    [InlineData("N042.00.28.000;E011.58.06.000;N041.59.26.000;E011.59.00.000;RESTRICT;E;", "RESTRICT", "E")]
    [InlineData("42.00777778 11.96833333 AREA E", "AREA", "E")]
    public void La_Lettera_Sola_Altrove_Resta_Un_Etichetta(string riga, string tipo, string nome)
    {
        var area = Assert.Single(CoordinateParser.Parse(riga).Aree);

        Assert.Equal(tipo, area.Tipo);
        Assert.Equal(nome, area.Nome);
    }
}
