using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Parser puro del file poligoni TWR Aurora (<c>twrs.tfl</c>): blocchi callsign + vertici DMS, chiusura su riga
/// vuota, conversione DMS→gradi decimali con segno.
/// </summary>
public class AuroraTowerShapeParserTests
{
    // Estratto reale (2 vertici per TWR): sufficiente a validare header/blocchi/DMS. Il parser richiede ≥3 punti,
    // quindi qui i blocchi usano 3 vertici (il primo ripetuto per chiudere l'anello).
    private const string Sample = """
        LIBA_TWR;TWR;1;TWR;1;
        N041.37.28.965;E015.43.18.960;
        N041.37.26.491;E015.43.58.078;
        N041.37.21.148;E015.44.36.682;

        LIBC_TWR;TWR;1;TWR;1;
        N039.05.48.116;E017.05.01.138;
        N039.05.45.845;E017.05.39.246;
        N039.05.41.125;E017.06.16.981;
        """;

    [Fact]
    public void Parses_Blocks_By_Callsign()
    {
        var map = AuroraSectorfileParser.ParseTowerShapes(Sample);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey("LIBA_TWR"));
        Assert.True(map.ContainsKey("LIBC_TWR"));
        Assert.Equal(3, map["LIBA_TWR"].Count);

        // Primo vertice LIBA: N041.37.28.965 / E015.43.18.960.
        var (lat, lon) = map["LIBA_TWR"][0];
        Assert.Equal(41.624713, lat, 5);
        Assert.Equal(15.721933, lon, 5);
    }

    [Fact]
    public void Ignores_Comment_Separator_Lines()
    {
        // Nel file reale le sezioni sono separate da righe "//----LIBB----": non devono diventare chiavi (0 punti → scartate).
        const string withComment = """
            //----------LIBB--------------
            LIBA_TWR;TWR;1;TWR;1;
            N041.37.28.965;E015.43.18.960;
            N041.37.26.491;E015.43.58.078;
            N041.37.21.148;E015.44.36.682;
            """;
        var map = AuroraSectorfileParser.ParseTowerShapes(withComment);
        Assert.Single(map);
        Assert.True(map.ContainsKey("LIBA_TWR"));
    }

    [Fact]
    public void Blocks_Under_Three_Points_Are_Dropped()
    {
        var map = AuroraSectorfileParser.ParseTowerShapes(
            "LIRN_TWR;TWR;1;TWR;1;\nN040.00.00.000;E014.00.00.000;\nN041.00.00.000;E015.00.00.000;\n");
        Assert.Empty(map);   // solo 2 punti → scartato
    }

    [Theory]
    [InlineData("N041.37.28.965", 41.624713)]
    [InlineData("E015.43.18.960", 15.721933)]
    [InlineData("S041.37.28.965", -41.624713)]
    [InlineData("W015.43.18.960", -15.721933)]
    public void Converts_Dms_To_Signed_Decimal(string token, double expected)
    {
        Assert.True(AuroraSectorfileParser.TryParseDms(token, out var d));
        Assert.Equal(expected, d, 5);
    }

    [Theory]
    [InlineData("X041.37.28.965")]   // emisfero non valido
    [InlineData("N041.37")]          // troppo corto
    [InlineData("")]
    public void Rejects_Malformed_Dms(string token)
    {
        Assert.False(AuroraSectorfileParser.TryParseDms(token, out _));
    }
}
