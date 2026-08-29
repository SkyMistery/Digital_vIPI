using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il righello. Le misure si provano su figure di cui il risultato si sa a mente: un grado di latitudine è
/// 60 NM per definizione, e un quadrato di un grado di lato all'equatore è 3600 NM².
/// </summary>
public class CoordinateGeometryTests
{
    [Fact]
    public void Un_Grado_Di_Latitudine_E_Sessanta_Miglia()
    {
        var p = new List<(double Lat, double Lon)> { (42.0, 11.0), (43.0, 11.0) };

        Assert.Equal(60.0, CoordinateGeometry.PerimetroNm(p, chiuso: false), 3);
    }

    [Fact]
    public void L_Anello_Chiuso_Conta_Anche_Il_Lato_Che_Torna_Indietro()
    {
        var p = new List<(double Lat, double Lon)> { (42.0, 11.0), (43.0, 11.0) };

        // Andata e ritorno: 120 NM. È la differenza fra una costa e un poligono.
        Assert.Equal(120.0, CoordinateGeometry.PerimetroNm(p, chiuso: true), 3);
    }

    [Fact]
    public void Il_Quadrato_Di_Un_Grado_All_Equatore_E_Tremilaseicento()
    {
        var p = new List<(double Lat, double Lon)> { (0.0, 0.0), (0.0, 1.0), (1.0, 1.0), (1.0, 0.0) };

        Assert.Equal(3600.0, CoordinateGeometry.AreaNm2(p), 0);
    }

    [Fact]
    public void L_Area_Non_Cambia_Se_L_Anello_Gira_Al_Contrario()
    {
        var p = new List<(double Lat, double Lon)> { (42.0, 11.0), (42.0, 12.0), (43.0, 12.0) };

        // ⚠️ Il verso è un'informazione, non un segno da mostrare.
        Assert.Equal(CoordinateGeometry.AreaNm2(p), CoordinateGeometry.AreaNm2(CoordinateGeometry.Inverti(p)), 6);
    }

    [Fact]
    public void Invertire_Un_Anello_Non_Sposta_Il_Punto_Di_Partenza()
    {
        var p = new List<(double Lat, double Lon)> { (42.0, 11.0), (43.0, 12.0), (44.0, 13.0) };

        var girato = CoordinateGeometry.Inverti(p, anelloChiuso: true);

        // ⚠️ Rovesciare l'elenco intero cambierebbe DUE cose: il verso e il punto di partenza. Il punto di
        // partenza ha già il suo gesto, e i due devono restare indipendenti.
        Assert.Equal((42.0, 11.0), girato[0]);
        Assert.Equal((44.0, 13.0), girato[1]);
        Assert.Equal((43.0, 12.0), girato[2]);
    }

    [Fact]
    public void Invertire_Una_Linea_Aperta_La_Percorre_Dall_Altro_Capo()
    {
        var p = new List<(double Lat, double Lon)> { (42.0, 11.0), (43.0, 12.0), (44.0, 13.0) };

        var girata = CoordinateGeometry.Inverti(p, anelloChiuso: false);

        // Su una costa il primo punto è un CAPO: invertire significa proprio cominciare dall'altro.
        Assert.Equal((44.0, 13.0), girata[0]);
        Assert.Equal((42.0, 11.0), girata[^1]);
    }

    [Fact]
    public void Meno_Di_Tre_Punti_Non_Fanno_Area() =>
        Assert.Equal(0, CoordinateGeometry.AreaNm2([(42.0, 11.0), (43.0, 12.0)]));

    [Fact]
    public void Il_Giro_Completo_Del_Committente_Non_Perde_Un_Metro()
    {
        var db = "42.00777778:11.96833333\n41.99055556:11.98333333\n41.94472222:11.98888889\n" +
                 "41.91666667:11.95833333\n41.975:11.92";
        var partenza = CoordinateParser.Parse(db).Aree[0].Punti;
        var testo = CoordinateWriter.Write(partenza, CoordinateOutput.SectorfileSegmenti,
            CoordinateWriteOptions.Default with { Nome = "R14A" });
        var tornati = CoordinateParser.Parse(testo).Aree[0].Punti;

        var errore = CoordinateGeometry.ErroreMassimoMetri(partenza, tornati);

        // Il DMS ha la risoluzione del millisecondo d'arco: ~3 cm. Sotto il decimetro è «zero» per chiunque.
        Assert.NotNull(errore);
        Assert.True(errore < 0.1, $"errore massimo {errore} m");
    }

    [Fact]
    public void Se_Il_Numero_Di_Punti_Cambia_Non_C_E_Un_Errore_Da_Dire()
    {
        // ⚠️ Non è «un errore grande»: è un'altra cosa, e un numero la nasconderebbe.
        Assert.Null(CoordinateGeometry.ErroreMassimoMetri([(42.0, 11.0)], [(42.0, 11.0), (43.0, 12.0)]));
    }

    [Fact]
    public void Ruotare_Cambia_Da_Dove_Si_Comincia_Non_La_Forma()
    {
        var p = new List<(double Lat, double Lon)> { (42.0, 11.0), (43.0, 12.0), (44.0, 13.0) };

        var ruotato = CoordinateGeometry.Ruota(p, 1);

        Assert.Equal((43.0, 12.0), ruotato[0]);
        Assert.Equal((42.0, 11.0), ruotato[^1]);
        Assert.Equal(CoordinateGeometry.AreaNm2(p), CoordinateGeometry.AreaNm2(ruotato), 6);
    }

    [Fact]
    public void Ruotare_Di_Un_Giro_Intero_Non_Fa_Niente()
    {
        var p = new List<(double Lat, double Lon)> { (42.0, 11.0), (43.0, 12.0), (44.0, 13.0) };

        Assert.Equal(p, CoordinateGeometry.Ruota(p, 3));
        Assert.Equal(p, CoordinateGeometry.Ruota(p, -3));
    }
}
