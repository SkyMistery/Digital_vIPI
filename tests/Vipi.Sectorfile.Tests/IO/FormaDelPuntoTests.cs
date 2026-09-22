using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Tests.IO;

/// <summary>
/// Carta F2, slice 2: una riga toccata si riscrive nella forma in cui i suoi punti erano scritti — puntata,
/// compatta o decimale — e senza marcatori. Righe vere dei campioni e del master del sector.
/// </summary>
public sealed class FormaDelPuntoTests
{
    [Fact]
    public void LeRigheDiconoLaLoroForma()
    {
        Assert.Equal(FormaDelPunto.Forma.Puntata, FormaDelPunto.Di(new[] { "N042.23.09.144;E013.18.34.590;N042.22.57.221;E013.18.35.124;;" }));
        Assert.Equal(FormaDelPunto.Forma.Compatta, FormaDelPunto.Di(new[] { "VICKY;PPE1;N0453745000;E0133332000;" }));
        Assert.Equal(FormaDelPunto.Forma.Decimale, FormaDelPunto.Di(new[] { "41.00850773;16.07432896;1B 6000;" }));
        Assert.Null(FormaDelPunto.Di(new[] { "AMSOR;AMSOR;", "LIRF;07;OST1E;;;;;1;" }));
    }

    [Fact]
    public void UnCommentoNonDecideLaForma()
        => Assert.Equal(FormaDelPunto.Forma.Compatta, FormaDelPunto.Di(new[]
        {
            "//N037.24.22.533;E014.54.59.095;N037.24.22.249;E014.54.58.966;PIER;",
            "N0414801000;E0121420000;",
        }));

    [Fact]
    public void LaCompattaVinceSoloSeEPrevalente()
        => Assert.Equal(FormaDelPunto.Forma.Puntata, FormaDelPunto.Di(new[]
        {
            "N041.48.01.000;E012.14.20.000;", "N041.48.02.000;E012.14.21.000;", "N0414801000;E0121420000;",
        }));

    [Fact]
    public void InCompattaIPuntiScrittiSiStringono()
        => Assert.Equal(
            new[] { "VICKY;PPE1;N0453745000;E0133332000;" },
            FormaDelPunto.In(new[] { "VICKY;PPE1;N045.37.45.000;E013.33.32.000;" }, FormaDelPunto.Forma.Compatta));

    [Fact]
    public void InDecimaleLaCoppiaDiventaDueNumeri()
        => Assert.Equal(
            new[] { "41.00850833;16.07432889;1B 6000;" },
            FormaDelPunto.In(new[] { "N041.00.30.630;E016.04.27.584;1B 6000;" }, FormaDelPunto.Forma.Decimale));

    [Fact]
    public void InPuntataNonCambiaNiente()
    {
        var righe = new[] { "N042.23.09.144;E013.18.34.590;" };

        Assert.Equal(righe, FormaDelPunto.In(righe, FormaDelPunto.Forma.Puntata));
    }

    // Solo un CAMPO intero è un punto: un'etichetta che ne contiene uno resta com'è.
    [Fact]
    public void UnPuntoDentroUnTestoNonSiTocca()
        => Assert.Equal(
            new[] { "L;X N045.37.45.000;N0453745000;E0133332000;" },
            FormaDelPunto.In(new[] { "L;X N045.37.45.000;N045.37.45.000;E013.33.32.000;" }, FormaDelPunto.Forma.Compatta));

    // Un .vfi è compatto: il punto spostato si riscrive compatto.
    [Fact]
    public void UnRecordCompattoSpostatoEsceCompatto()
    {
        string path = Path.Combine(Path.GetTempPath(), "forma-" + Guid.NewGuid().ToString("N") + ".vfi");
        File.WriteAllText(path, "VICKY;PPE1;N0453745000;E0133332000;\r\n");
        try
        {
            var warnings = new Vipi.Sectorfile.IO.Tests.CollectingWarnings();
            var letto = new VfiParser(warnings).Parse(path, new ColorPalette()).FissaLeBasi(new VfiSaver());
            var punto = letto.Records[0];
            punto.Position = new Coordinate(punto.Position.LatitudeDeg + 1 / 3600.0, punto.Position.LongitudeDeg);

            new FileSaverOrchestrator().Save(letto, new HashSet<VfrPoint>(letto.Records), new VfiSaver(), path);

            Assert.Equal("VICKY;PPE1;N0453746000;E0133332000;\r\n", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
