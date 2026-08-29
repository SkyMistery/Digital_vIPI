using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il lettore «qualsiasi formato» della carta del 29 agosto 2026: un caso per forma riconosciuta, più i casi
/// storti — che sono il vero motivo per cui esiste la diagnostica.
/// </summary>
public class CoordinateParserTests
{
    // Il primo vertice dell'area R14A, l'esempio del committente. Ogni forma qui sotto dice LO STESSO PUNTO.
    private const double Lat = 42.00777778;
    private const double Lon = 11.96833333;

    [Theory]
    [InlineData("N042.00.28.000;E011.58.06.000;")]          // sectorfile a punti
    [InlineData("N0420028000;E0115806000;")]                // sectorfile compatto
    [InlineData("42.00777778:11.96833333")]                 // DB IVAO
    [InlineData("42.00777778, 11.96833333")]                // CSV / Google Maps
    [InlineData("42.00777778 11.96833333")]                 // due numeri e uno spazio
    [InlineData("42°0'28\"N 11°58'6\"E")]                   // DMS coi simboli, emisfero dietro
    [InlineData("N42°0'28\" E11°58'6\"")]                   // DMS coi simboli, emisfero davanti
    [InlineData("42 00 28 N 11 58 06 E")]                   // DMS a spazi
    [InlineData("42:00:28N 11:58:06E")]                     // DMS coi due punti
    [InlineData("420028N0115806E")]                         // ARINC DDMMSS
    [InlineData("N420028E0115806")]                         // ARINC con emisfero davanti
    [InlineData("42.00777778N 11.96833333E")]               // decimale con emisfero
    [InlineData("N42.00777778 E11.96833333")]
    public void Riconosce_Lo_Stesso_Punto_In_Tutte_Le_Forme(string riga)
    {
        var esito = CoordinateParser.Parse(riga);

        var area = Assert.Single(esito.Aree);
        var p = Assert.Single(area.Punti);
        Assert.Equal(Lat, p.Lat, 5);
        Assert.Equal(Lon, p.Lon, 5);
        Assert.Empty(esito.Segnalazioni);
    }

    [Fact]
    public void Il_Sud_E_L_Ovest_Sono_Negativi()
    {
        var esito = CoordinateParser.Parse("S042.00.28.000;W011.58.06.000;\n-42.00777778:-11.96833333");

        var area = Assert.Single(esito.Aree);
        Assert.Equal(2, area.Punti.Count);
        Assert.All(area.Punti, p =>
        {
            Assert.Equal(-Lat, p.Lat, 5);
            Assert.Equal(-Lon, p.Lon, 5);
        });
    }

    [Fact]
    public void I_Primi_Decimali_Si_Leggono_Come_Tali()
    {
        // 42°00.4667' = 42.007778°, la forma delle carte aeronautiche.
        var esito = CoordinateParser.Parse("42°00.4667'N 011°58.1'E");

        var p = Assert.Single(Assert.Single(esito.Aree).Punti);
        Assert.Equal(Lat, p.Lat, 5);
        Assert.Equal(Lon, p.Lon, 5);
    }

    [Fact]
    public void Il_Json_Ha_La_Longitudine_Prima()
    {
        // ⚠️ Regola IVAO regionMapPolygon: [lng, lat]. Invertirla darebbe un poligono ruotato di 90° che
        // nessuno segnala, perché si disegna benissimo.
        var esito = CoordinateParser.Parse("[[11.96833333,42.00777778],[11.98333333,41.99055556]]");

        var area = Assert.Single(esito.Aree);
        Assert.Equal(2, area.Punti.Count);
        Assert.Equal(Lat, area.Punti[0].Lat, 5);
        Assert.Equal(Lon, area.Punti[0].Lon, 5);
    }

    [Fact]
    public void Il_Primo_Numero_Oltre_90_Si_Scambia_E_Lo_Dice()
    {
        var esito = CoordinateParser.Parse("111.96833333, 42.00777778");

        var p = Assert.Single(Assert.Single(esito.Aree).Punti);
        Assert.Equal(42.00777778, p.Lat, 5);
        Assert.Equal(111.96833333, p.Lon, 5);
        Assert.Contains(esito.Segnalazioni, s => s.Kind == CoordinateIssueKind.LatLonScambiate);
    }

    [Fact]
    public void La_Riga_Illeggibile_Esce_Col_Suo_Numero()
    {
        var esito = CoordinateParser.Parse("42.00777778:11.96833333\nquesta non e' una coordinata\n41.975:11.92");

        Assert.Equal(2, Assert.Single(esito.Aree).Punti.Count);
        var problema = Assert.Single(esito.Segnalazioni);
        Assert.Equal(CoordinateIssueKind.RigaNonLetta, problema.Kind);
        Assert.Equal(2, problema.Riga);
        Assert.Equal("questa non e' una coordinata", problema.Testo);
        Assert.Equal(2, esito.RigheLette);
        Assert.Equal(3, esito.RigheTotali);
    }

    [Fact]
    public void I_Commenti_Non_Sono_Righe_Perse()
    {
        // Gli header dei .geo sono commenti: scartarli è giusto, ma segnalarli sarebbe rumore.
        var esito = CoordinateParser.Parse("//PENISOLA\n42.00777778:11.96833333\n# nota\n41.975:11.92");

        Assert.Equal(2, Assert.Single(esito.Aree).Punti.Count);
        Assert.Empty(esito.Segnalazioni);
    }

    [Fact]
    public void L_Angolo_Spaiato_Si_Segnala()
    {
        var esito = CoordinateParser.Parse("42.00777778:11.96833333:41.975");

        Assert.Contains(esito.Segnalazioni, s => s.Kind == CoordinateIssueKind.AngoloSpaiato);
        Assert.Single(Assert.Single(esito.Aree).Punti);
    }

    [Fact]
    public void La_Latitudine_Oltre_90_E_Fuori_Intervallo()
    {
        var esito = CoordinateParser.Parse("N095.00.00.000;E011.58.06.000;");

        Assert.Empty(esito.Aree);
        Assert.Contains(esito.Segnalazioni, s => s.Kind == CoordinateIssueKind.FuoriIntervallo);
    }

    // ---- Il sectorfile a segmenti: la forma dell'esempio del committente ----

    private const string R14A =
        "N042.00.28.000;E011.58.06.000;N041.59.26.000;E011.59.00.000;RESTRICT;R14A;\n" +
        "N041.59.26.000;E011.59.00.000;N041.56.41.000;E011.59.20.000;RESTRICT;R14A;\n" +
        "N041.56.41.000;E011.59.20.000;N041.55.00.000;E011.57.30.000;RESTRICT;R14A;\n" +
        "N041.55.00.000;E011.57.30.000;N041.58.30.000;E011.55.12.000;RESTRICT;R14A;\n" +
        "N041.58.30.000;E011.55.12.000;N042.00.28.000;E011.58.06.000;RESTRICT;R14A;";

    [Fact]
    public void I_Segmenti_Diventano_Cinque_Vertici_E_Un_Anello_Chiuso()
    {
        var esito = CoordinateParser.Parse(R14A);

        var area = Assert.Single(esito.Aree);
        Assert.Equal("R14A", area.Nome);
        Assert.Equal("RESTRICT", area.Tipo);
        Assert.True(area.DaSegmenti);
        Assert.True(area.AnelloChiuso);
        // ⚠️ CINQUE, non sei: il vertice di chiusura è una proprietà dell'anello, non un punto in più.
        Assert.Equal(5, area.Punti.Count);
        Assert.Equal(42.00777778, area.Punti[0].Lat, 6);
        Assert.Equal(41.975, area.Punti[4].Lat, 6);
        Assert.Empty(esito.Segnalazioni);
    }

    [Fact]
    public void La_Catena_Interrotta_Si_Segnala_E_Non_Si_Aggiusta()
    {
        var rotta =
            "N042.00.28.000;E011.58.06.000;N041.59.26.000;E011.59.00.000;RESTRICT;R14A;\n" +
            "N041.50.00.000;E011.50.00.000;N041.56.41.000;E011.59.20.000;RESTRICT;R14A;";

        var esito = CoordinateParser.Parse(rotta);

        var salto = Assert.Single(esito.Segnalazioni);
        Assert.Equal(CoordinateIssueKind.CatenaInterrotta, salto.Kind);
        Assert.Equal(2, salto.Riga);
        Assert.Equal(3, Assert.Single(esito.Aree).Punti.Count);   // i punti restano tutti: il salto si vede in mappa
    }

    [Fact]
    public void Due_Aree_Nello_Stesso_Testo_Sono_Due_Aree()
    {
        var due =
            "N042.00.28.000;E011.58.06.000;N041.59.26.000;E011.59.00.000;RESTRICT;R14A;\n" +
            "N043.00.00.000;E012.00.00.000;N043.10.00.000;E012.10.00.000;RESTRICT;R107B;\n" +
            "N041.59.26.000;E011.59.00.000;N041.56.41.000;E011.59.20.000;RESTRICT;R14A;";

        var esito = CoordinateParser.Parse(due);

        // ⚠️ Il raggruppamento è per NOME, non per posizione: in italy.restrict le righe si alternano davvero.
        Assert.Equal(2, esito.Aree.Count);
        Assert.Equal("R14A", esito.Aree[0].Nome);
        Assert.Equal(3, esito.Aree[0].Punti.Count);
        Assert.Equal("R107B", esito.Aree[1].Nome);
    }

    [Fact]
    public void La_Riga_Vuota_Separa_Due_Blocchi_Anonimi()
    {
        var esito = CoordinateParser.Parse("42:11\n42.5:11.5\n\n43:12\n43.5:12.5");

        Assert.Equal(2, esito.Aree.Count);
        Assert.All(esito.Aree, a => Assert.Equal(2, a.Punti.Count));
        Assert.All(esito.Aree, a => Assert.Null(a.Nome));
    }

    [Fact]
    public void Il_Geo_Ha_Il_Tipo_E_Non_Il_Nome()
    {
        var esito = CoordinateParser.Parse(
            "N0434857348;E0072851261;N0435758897;E0073447218;COAST;\n" +
            "N0435758897;E0073447218;N0440243373;E0074122725;COAST;");

        var area = Assert.Single(esito.Aree);
        Assert.Equal("COAST", area.Tipo);
        Assert.Null(area.Nome);
        Assert.Equal(3, area.Punti.Count);
        Assert.False(area.AnelloChiuso);   // una costa non è un poligono
    }

    [Fact]
    public void Il_Vertice_Di_Chiusura_Ripetuto_Non_Diventa_Un_Punto_In_Piu()
    {
        var esito = CoordinateParser.Parse("42:11\n43:12\n44:13\n42:11");

        var area = Assert.Single(esito.Aree);
        Assert.True(area.AnelloChiuso);
        Assert.Equal(3, area.Punti.Count);
    }

    [Fact]
    public void Fuori_Dall_Italia_Si_Nota()
    {
        Assert.True(CoordinateParser.Parse("52.5:13.4\n52.6:13.5").TuttoFuoriItalia);      // Berlino
        Assert.False(CoordinateParser.Parse("42.00777778:11.96833333").TuttoFuoriItalia);
    }

    [Fact]
    public void Oltre_Il_Tetto_Di_Righe_Si_Ferma_E_Lo_Dice()
    {
        var testo = string.Join("\n", Enumerable.Repeat("42.0:11.0", CoordinateParser.MaxRighe + 10));

        var esito = CoordinateParser.Parse(testo);

        Assert.Contains(esito.Segnalazioni, s => s.Kind == CoordinateIssueKind.TroppeRighe);
        Assert.Equal(CoordinateParser.MaxRighe, esito.RigheLette);
    }

    [Fact]
    public void Il_Vuoto_Non_E_Un_Errore() => Assert.Empty(CoordinateParser.Parse("   ").Aree);
}
