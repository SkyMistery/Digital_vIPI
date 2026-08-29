using System.IO.Compression;
using System.Text;
using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// KML e KMZ: il formato con cui arrivano le aree disegnate in Google Earth. ⚠️ Le coordinate sono
/// <c>lon,lat,alt</c>, e sbagliare quell'ordine produce un poligono ruotato che nessuno segnala.
/// </summary>
public class KmlReaderTests
{
    /// <summary>Un KML come lo scrive Google Earth: spazio dei nomi OGC, anello chiuso, quota a zero.</summary>
    private const string R14A = """
        <?xml version="1.0" encoding="UTF-8"?>
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Placemark>
              <name>R14A</name>
              <Polygon><outerBoundaryIs><LinearRing><coordinates>
                11.96833333,42.00777778,0
                11.98333333,41.99055556,0
                11.98888889,41.94472222,0
                11.95833333,41.91666667,0
                11.92,41.975,0
                11.96833333,42.00777778,0
              </coordinates></LinearRing></outerBoundaryIs></Polygon>
            </Placemark>
          </Document>
        </kml>
        """;

    [Fact]
    public void Legge_Il_Poligono_Col_Nome_Del_Placemark()
    {
        var esito = KmlReader.LeggiKml(R14A);

        var area = Assert.Single(esito.Aree);
        Assert.Equal("R14A", area.Nome);
        Assert.True(area.AnelloChiuso);
        Assert.Equal(5, area.Punti.Count);            // il vertice ripetuto non è un punto in più
        Assert.Equal(42.00777778, area.Punti[0].Lat, 6);
        Assert.Equal(11.96833333, area.Punti[0].Lon, 6);
        Assert.Empty(esito.Segnalazioni);
    }

    [Fact]
    public void Il_Buco_Si_Scarta_E_Lo_Dice()
    {
        var conBuco = R14A.Replace("</Polygon>", """
            <innerBoundaryIs><LinearRing><coordinates>
              11.96,41.97,0 11.97,41.97,0 11.97,41.98,0 11.96,41.97,0
            </coordinates></LinearRing></innerBoundaryIs></Polygon>
            """);

        var esito = KmlReader.LeggiKml(conBuco);

        var area = Assert.Single(esito.Aree);
        Assert.Equal(5, area.Punti.Count);            // resta il contorno esterno, intero
        var avviso = Assert.Single(esito.Segnalazioni);
        Assert.Equal(CoordinateIssueKind.BucoScartato, avviso.Kind);
        Assert.Equal("R14A", avviso.Testo);
    }

    [Fact]
    public void Piu_Placemark_Sono_Piu_Aree()
    {
        var due = R14A.Replace("</Document>", """
            <Placemark><name>R107B</name><LineString><coordinates>
              12.0,43.0,0 12.1,43.1,0
            </coordinates></LineString></Placemark></Document>
            """);

        var esito = KmlReader.LeggiKml(due);

        Assert.Equal(2, esito.Aree.Count);
        Assert.Equal("R14A", esito.Aree[0].Nome);
        Assert.Equal("R107B", esito.Aree[1].Nome);
        Assert.False(esito.Aree[1].AnelloChiuso);     // una LineString non è un anello
    }

    [Fact]
    public void Lo_Spazio_Dei_Nomi_Non_Conta()
    {
        // Un KML senza dichiarazione di namespace, e uno con quello di Google: entrambi si leggono.
        var nudo = R14A.Replace(" xmlns=\"http://www.opengis.net/kml/2.2\"", "");
        Assert.Single(KmlReader.LeggiKml(nudo).Aree);

        var google = R14A.Replace("http://www.opengis.net/kml/2.2", "http://earth.google.com/kml/2.1");
        Assert.Single(KmlReader.LeggiKml(google).Aree);
    }

    [Fact]
    public void Il_Placemark_Senza_Nome_Resta_Senza_Nome()
    {
        var anonimo = R14A.Replace("<name>R14A</name>", "");
        Assert.Null(Assert.Single(KmlReader.LeggiKml(anonimo).Aree).Nome);
    }

    [Fact]
    public void L_Xml_Rotto_Non_Fa_Cadere_Niente()
    {
        var esito = KmlReader.LeggiKml("<kml><Placemark><name>rotto");

        Assert.Empty(esito.Aree);
        Assert.Equal(CoordinateIssueKind.FileNonLetto, Assert.Single(esito.Segnalazioni).Kind);
    }

    [Fact]
    public void Un_Kml_Senza_Placemark_Lo_Dice()
    {
        var esito = KmlReader.LeggiKml("<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document/></kml>");

        Assert.Empty(esito.Aree);
        Assert.Equal(CoordinateIssueKind.FileNonLetto, Assert.Single(esito.Segnalazioni).Kind);
    }

    [Fact]
    public void Il_Kml_Incollato_Nel_Riquadro_Passa_Dallo_Stesso_Lettore()
    {
        // ⚠️ Il KML non è un secondo dispatch: chi incolla l'XML nel riquadro ottiene le stesse aree.
        var esito = CoordinateParser.Parse(R14A);

        Assert.Equal("R14A", Assert.Single(esito.Aree).Nome);
    }

    // ---- KMZ: uno zip con dentro il KML ----

    private static MemoryStream Kmz(string nomeVoce, string contenuto, int voci = 1)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 1; i < voci; i++)
            {
                using var extra = new StreamWriter(zip.CreateEntry($"files/immagine{i}.png").Open());
                extra.Write("x");
            }
            using var w = new StreamWriter(zip.CreateEntry(nomeVoce).Open(), Encoding.UTF8);
            w.Write(contenuto);
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Il_Kmz_E_Un_Kml_Dentro_Uno_Zip()
    {
        using var kmz = Kmz("doc.kml", R14A);

        var esito = KmlReader.LeggiKmz(kmz);

        Assert.Equal("R14A", Assert.Single(esito.Aree).Nome);
    }

    [Fact]
    public void Senza_Doc_Kml_Vale_Il_Primo_Kml()
    {
        using var kmz = Kmz("aree/italia.kml", R14A);
        Assert.Single(KmlReader.LeggiKmz(kmz).Aree);
    }

    [Fact]
    public void Uno_Zip_Senza_Kml_Non_E_Un_Kmz()
    {
        using var kmz = Kmz("lettera.txt", "niente coordinate qui");

        var esito = KmlReader.LeggiKmz(kmz);

        Assert.Empty(esito.Aree);
        Assert.Equal(CoordinateIssueKind.FileNonLetto, Assert.Single(esito.Segnalazioni).Kind);
    }

    [Fact]
    public void Troppe_Voci_Nello_Zip_Si_Rifiutano()
    {
        using var kmz = Kmz("doc.kml", R14A, voci: KmlReader.MaxVociZip + 2);

        var esito = KmlReader.LeggiKmz(kmz);

        Assert.Empty(esito.Aree);
        Assert.Contains("voci", Assert.Single(esito.Segnalazioni).Dettaglio);
    }

    [Fact]
    public void Un_File_Che_Zip_Non_E_Non_Fa_Cadere_Niente()
    {
        using var finto = new MemoryStream(Encoding.UTF8.GetBytes("non sono uno zip"));

        var esito = KmlReader.LeggiKmz(finto);

        Assert.Empty(esito.Aree);
        Assert.Equal(CoordinateIssueKind.FileNonLetto, Assert.Single(esito.Segnalazioni).Kind);
    }
}
