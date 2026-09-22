using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Tests.Shared;

/// <summary>
/// Carta F2, slice 2: come il motore legge un punto, allineato al DMS di vIPI (<c>DmsCoordinate</c>). I casi
/// sono token VERI del sector (master <c>7e761aa</c>) su cui la libreria A sbagliava; la concordanza su tutti
/// i 685 561 token dell'albero la misura <c>tools/Vipi.SectorfileProva</c>.
/// </summary>
public sealed class LetturaDelPuntoTests
{
    private const double UnDecimillesimoDiSecondo = 1e-4 / 3600;

    // DYNAMIC_SEC/lovv.tfl:70 — A leggeva «8735» come millisecondi: 33,735 s, circa 270 m più in là.
    [Fact]
    public void LaFrazioneDeiSecondiEUnaFrazioneAQuattroCifre()
        => Assert.Equal(46 + 34 / 60.0 + 25.8735 / 3600, CoordinateConverter.Parse("N046.34.25.8735").LatitudeDeg, UnDecimillesimoDiSecondo);

    // OTHER/limm.rw:49, lipp.rw:58, lirr.rw:89 — una soglia pista: A dava 55,072 s invece di 55,72.
    [Fact]
    public void LaFrazioneDeiSecondiEUnaFrazioneADueCifre()
        => Assert.Equal(44 + 18 / 60.0 + 55.72 / 3600, CoordinateConverter.Parse("N44.18.55.72").LatitudeDeg, UnDecimillesimoDiSecondo);

    // libf.str:108, licc.str:218, lipk.str:71 — «07.1000» sono 7,1 secondi, non 8.
    [Fact]
    public void MilleDopoIlPuntoNonEUnSecondoInPiu()
        => Assert.Equal(15 + 37 / 60.0 + 7.1 / 3600, CoordinateConverter.Parse("E015.37.07.1000").LongitudeDeg, UnDecimillesimoDiSecondo);

    [Fact]
    public void SenzaFrazioneSiLeggeComeVipi()
        => Assert.Equal(41 + 37 / 60.0 + 28 / 3600.0, CoordinateConverter.Parse("N041.37.28").LatitudeDeg, UnDecimillesimoDiSecondo);

    // NAVAIDS/itvor.vor:125 — A perdeva la riga intera del VOR VBA. Il minuscolo lo segnala il validatore.
    [Fact]
    public void LEmisferoMinuscoloSiLegge()
        => Assert.Equal(45 + 44 / 60.0 + 52.08 / 3600, CoordinateConverter.Parse("n045.44.52.080").LatitudeDeg, UnDecimillesimoDiSecondo);

    [Theory]
    [InlineData("N090.00.00.001")]
    [InlineData("N095.00.00.000")]
    [InlineData("E180.00.00.001")]
    [InlineData("N0950000000")]
    [InlineData("N041.60.00.000")]
    [InlineData("N041.37.60.000")]
    public void FuoriIntervalloSiRifiuta(string token)
        => Assert.Throws<CoordinateParseException>(() => CoordinateConverter.Parse(token));

    [Theory]
    [InlineData("N090.00.00.000")]
    [InlineData("W180.00.00.000")]
    public void IlTettoEsattoSiAccetta(string token)
        => CoordinateConverter.Parse(token);

    // liba/libd/lict/lire.str, 38 righe: in A la longitudine usciva 0, senza nessun avviso.
    [Fact]
    public void UnaCoppiaDecimaleHaLaSuaLongitudine()
    {
        var punto = CoordinateConverter.ParsePair("41.00850773", "16.07432896");

        Assert.Equal(41.00850773, punto.LatitudeDeg, 1e-9);
        Assert.Equal(16.07432896, punto.LongitudeDeg, 1e-9);
    }

    [Fact]
    public void UnaCoppiaDmsHaIDueAssi()
    {
        var punto = CoordinateConverter.ParsePair("S015.30.00.000", "W079.00.00.000");

        Assert.Equal(-15.5, punto.LatitudeDeg, 1e-9);
        Assert.Equal(-79.0, punto.LongitudeDeg, 1e-9);
    }

    [Theory]
    [InlineData("E012.14.20.000", "N041.48.01.000")]    // assi scambiati: in A diventava 0,0
    [InlineData("N041.48.01.000", "N012.14.20.000")]    // due latitudini
    [InlineData("41.8", "E012.14.20.000")]              // decimale e DMS nello stesso punto: il formato lo vieta
    [InlineData("N041.48.01.000", "12.2")]
    [InlineData("91.0", "12.0")]                        // decimali fuori intervallo
    [InlineData("41.0", "181.0")]
    [InlineData("N041.48.01.000", "AMSOR")]             // DMS e nome mescolati
    [InlineData("", "E012.14.20.000")]
    public void UnaCoppiaCheNonEUnPuntoSiRifiuta(string lat, string lon)
        => Assert.Throws<CoordinateParseException>(() => CoordinateConverter.ParsePair(lat, lon));
}
