using System.IO.Compression;
using System.Text;
using Vipi.Application.Airspace;

namespace Vipi.Application.Tests;

/// <summary>
/// Il lettore degli spazi aerei dell'AIP. ⚠️ Il file non contiene contorni ma <b>scatole</b>: tetto, pavimento
/// e una parete per lato. Chi le legge come aree distinte si ritrova <c>TMA MILANO Z1 (1)…(147)</c>.
/// <para>La fixture <c>Fixtures/spazi-aerei-ritaglio.kml</c> è ritagliata dal file vero del 15 luglio 2026:
/// dieci volumi scelti perché coprono i casi, più un VOR che deve restare fuori.</para>
/// </summary>
public class AirspaceKmlReaderTests
{
    private static AirspaceReadResult Ritaglio() =>
        AirspaceKmlReader.LeggiKml(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "spazi-aerei-ritaglio.kml")));

    // Una scatola come la scrive AirspaceConverter: tetto e pavimento sullo stesso contorno a due quote, più
    // una parete per lato. Il contorno è un triangolo, quindi le pareti sono tre.
    private const string Scatola = """
        <?xml version="1.0" encoding="UTF-8"?>
        <kml xmlns="http://www.opengis.net/kml/2.2"><Document>
          <Placemark>
            <name>PROVA CTR</name>
            <ExtendedData><SchemaData schemaUrl="#AirspaceId">
              <SimpleData name="Name">PROVA CTR</SimpleData>
              <SimpleData name="Category">Control Traffic Region</SimpleData>
              <SimpleData name="Top">FL105</SimpleData>
              <SimpleData name="Base">2000 FT AMSL</SimpleData>
            </SchemaData></ExtendedData>
            <MultiGeometry>
              <Polygon><altitudeMode>absolute</altitudeMode><outerBoundaryIs><LinearRing><coordinates>
                9.0,45.0,3200.4 9.5,45.0,3200.4 9.25,45.5,3200.4 9.0,45.0,3200.4
              </coordinates></LinearRing></outerBoundaryIs></Polygon>
              <Polygon><altitudeMode>absolute</altitudeMode><outerBoundaryIs><LinearRing><coordinates>
                9.0,45.0,609.6 9.5,45.0,609.6 9.25,45.5,609.6 9.0,45.0,609.6
              </coordinates></LinearRing></outerBoundaryIs></Polygon>
              <Polygon><altitudeMode>absolute</altitudeMode><outerBoundaryIs><LinearRing><coordinates>
                9.0,45.0,3200.4 9.5,45.0,3200.4 9.5,45.0,609.6 9.0,45.0,609.6 9.0,45.0,3200.4
              </coordinates></LinearRing></outerBoundaryIs></Polygon>
              <Polygon><altitudeMode>absolute</altitudeMode><outerBoundaryIs><LinearRing><coordinates>
                9.5,45.0,3200.4 9.25,45.5,3200.4 9.25,45.5,609.6 9.5,45.0,609.6 9.5,45.0,3200.4
              </coordinates></LinearRing></outerBoundaryIs></Polygon>
              <Polygon><altitudeMode>absolute</altitudeMode><outerBoundaryIs><LinearRing><coordinates>
                9.25,45.5,3200.4 9.0,45.0,3200.4 9.0,45.0,609.6 9.25,45.5,609.6 9.25,45.5,3200.4
              </coordinates></LinearRing></outerBoundaryIs></Polygon>
            </MultiGeometry>
          </Placemark>
        </Document></kml>
        """;

    [Fact]
    public void Una_Scatola_E_Un_Volume_Solo_Non_Cinque_Aree()
    {
        var esito = AirspaceKmlReader.LeggiKml(Scatola);

        var volume = Assert.Single(esito.Volumes);
        Assert.Equal("PROVA CTR", volume.Name);
        Assert.Single(volume.Rings);              // cinque poligoni, un contorno
        Assert.Equal(3, volume.PointCount);       // il vertice di chiusura non è un punto in più
        Assert.Equal(AirspaceFamily.Ctr, volume.Family);
    }

    [Fact]
    public void Le_Quote_Vengono_Dai_Campi_Non_Dalle_Coordinate()
    {
        // ⚠️ Nelle coordinate ci sono 3200,4 e 609,6 METRI. Leggere lì dentro darebbe una misura che somiglia
        // a quella giusta ed è un'altra cosa: la verità sta nei due campi di testo.
        var volume = Assert.Single(AirspaceKmlReader.LeggiKml(Scatola).Volumes);

        Assert.Equal(AirspaceDatum.Amsl, volume.Base.Datum);
        Assert.Equal(2000, volume.Base.Feet);
        Assert.Equal("2000 FT AMSL", volume.Base.Raw);
        Assert.Equal(AirspaceDatum.FlightLevel, volume.Top.Datum);
        Assert.Equal(10500, volume.Top.Feet);
    }

    [Fact]
    public void Il_Ritaglio_Vero_Da_Dieci_Volumi_E_Un_Anello_Per_Ciascuno()
    {
        var esito = Ritaglio();

        Assert.Equal(10, esito.PlacemarksRead);     // il VOR non è uno spazio aereo
        Assert.Equal(10, esito.Volumes.Count);
        Assert.All(esito.Volumes, v => Assert.Single(v.Rings));
        Assert.DoesNotContain(esito.Issues, i => i.Kind == AirspaceIssueKind.VolumeAPiuAnelli);
        Assert.DoesNotContain(esito.Issues, i => i.Kind == AirspaceIssueKind.VolumeSenzaAnello);
    }

    [Fact]
    public void Il_Punto_Dappoggio_Resta_Fuori()
    {
        // Il VOR di Alghero ha un Placemark come tutti, e nessuna `Category`: è così che si riconosce uno
        // spazio aereo. Non dal fatto che abbia un poligono — anche i campi ne hanno uno, ed è la pista.
        Assert.DoesNotContain(Ritaglio().Volumes, v => v.Name.Contains("ALGHERO", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void La_Scatola_Da_Ventidue_Poligoni_Resta_Un_Anello()
    {
        var z9 = Ritaglio().Volumes.Where(v => v.Name == "CTA ROMA Z9 GOLFO MANFREDONIA").ToList();

        Assert.Equal(2, z9.Count);
        Assert.All(z9, v => Assert.Single(v.Rings));
        Assert.Equal([4, 20], z9.Select(v => v.PointCount).OrderBy(n => n).ToArray());
    }

    [Fact]
    public void Due_Volumi_Con_La_Stessa_Chiave_Il_Secondo_Prende_Lordinale()
    {
        var esito = Ritaglio();
        var z9 = esito.Volumes.Where(v => v.Name == "CTA ROMA Z9 GOLFO MANFREDONIA").ToList();

        Assert.Equal(z9[0].NaturalKey, z9[1].NaturalKey);          // nome, base e tetto identici
        Assert.Equal([0, 1], z9.Select(v => v.Ordinal).ToArray());
        Assert.Contains(esito.Issues, i => i.Kind == AirspaceIssueKind.ChiaveDuplicata);
    }

    [Fact]
    public void Lo_Stesso_Nome_Con_Bande_Diverse_Fa_Due_Chiavi()
    {
        // ⚠️ GRAZZANISE CTR Z2 c'è due volte: GND→1500 AGL e 1500 AGL→FL125. Sono due volumi diversi, e col
        // solo nome per chiave il secondo avrebbe cancellato il primo.
        var g = Ritaglio().Volumes.Where(v => v.Name == "GRAZZANISE CTR Z2").ToList();

        Assert.Equal(2, g.Count);
        Assert.NotEqual(g[0].NaturalKey, g[1].NaturalKey);
        Assert.All(g, v => Assert.Equal(0, v.Ordinal));
    }

    [Fact]
    public void Le_Famiglie_Del_Ritaglio_Sono_Quelle_Misurate()
    {
        var per = Ritaglio().Volumes.GroupBy(v => v.Family).ToDictionary(g => g.Key, g => g.Count());

        // ⚠️ PISA CTR Z3 sta fra i CTR pur essendo di classe C: sulle classi decide il nome, non la categoria.
        Assert.Equal(4, per[AirspaceFamily.Ctr]);          // PISA Z3, BOLOGNA Z7, GRAZZANISE ×2
        Assert.Equal(3, per[AirspaceFamily.Cta]);          // CTA MILANO Z28, CTA ROMA Z9 ×2
        Assert.Equal(1, per[AirspaceFamily.Atz]);
        Assert.Equal(1, per[AirspaceFamily.Restricted]);
        Assert.Equal(1, per[AirspaceFamily.Other]);
    }

    [Fact]
    public void Le_Aree_Regolamentate_Si_Leggono_Ma_Non_Si_Usano()
    {
        // Il committente ha deciso il 29 agosto 2026 che R, P, D e le altre aree vengono solo dal catalogo
        // IVAO. Il file però si legge intero: restano in catalogo con detto perché.
        var esito = Ritaglio();

        var sara = Assert.Single(esito.Volumes, v => v.Name == "LI-R21/B-SARA");
        Assert.Equal(AirspaceFamily.Restricted, sara.Family);
        Assert.False(sara.IsUsable);
        Assert.DoesNotContain(esito.Usable, v => v.Name == "LI-R21/B-SARA");
    }

    [Fact]
    public void Un_Kmz_Si_Legge_Come_Il_Kml_Che_Ha_Dentro()
    {
        using var zip = new MemoryStream();
        using (var archivio = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        using (var voce = new StreamWriter(archivio.CreateEntry("doc.kml").Open(), Encoding.UTF8))
            voce.Write(Scatola);
        zip.Position = 0;

        var volume = Assert.Single(AirspaceKmlReader.LeggiKmz(zip).Volumes);
        Assert.Equal("PROVA CTR", volume.Name);
    }

    [Fact]
    public void Un_File_Senza_Spazi_Aerei_Lo_Dice()
    {
        var esito = AirspaceKmlReader.LeggiKml(
            """<?xml version="1.0"?><kml xmlns="http://www.opengis.net/kml/2.2"><Document/></kml>""");

        Assert.Empty(esito.Volumes);
        Assert.Contains(esito.Issues, i => i.Kind == AirspaceIssueKind.FileNonLetto);
    }

    [Fact]
    public void Un_Volume_Senza_Contorno_Si_Segnala_Invece_Di_Sparire()
    {
        var esito = AirspaceKmlReader.LeggiKml("""
            <?xml version="1.0"?><kml xmlns="http://www.opengis.net/kml/2.2"><Document>
              <Placemark><ExtendedData><SchemaData>
                <SimpleData name="Name">SENZA FORMA</SimpleData>
                <SimpleData name="Category">Control Traffic Region</SimpleData>
                <SimpleData name="Base">GND</SimpleData><SimpleData name="Top">FL100</SimpleData>
              </SchemaData></ExtendedData></Placemark>
            </Document></kml>
            """);

        Assert.Empty(esito.Volumes);
        Assert.Equal(1, esito.PlacemarksRead);
        var segnalazione = Assert.Single(esito.Issues);
        Assert.Equal(AirspaceIssueKind.VolumeSenzaAnello, segnalazione.Kind);
        Assert.Equal("SENZA FORMA", segnalazione.Volume);
    }
}
