using Vipi.Sectorfile.IO;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>I simboli del <c>.sym</c> (lotto «Subito» slice 4d): righe vere di <c>symbols.sym</c> del fork.</summary>
public sealed class SymParserTests
{
    private const string Apt = "0000000000000;0000011100000;0000100010000;0001000001000;0010000000100;0010000000100;1111111111111;0010000000100;0010000000100;0001000001000;0000100010000;0000011100000;0000000000000;";
    private const string Fix = "0000000100000;0000001100000;0000010100000;0000100100000;0001000100000;0010000100000;0100000100000;0010000100000;0001000100000;0000100100000;0000010100000;0000001100000;0000000100000;";

    private readonly CollectingWarnings _warnings = new();
    private SymParser Parser => new(_warnings);

    [Fact]
    public void OgniGruppoEUnaColonna_IlFixPuntaInAlto()
    {
        var fix = Assert.Single(Parser.Parse(ParserTestHelpers.Read("//FIX vuoto\r\n" + Fix + "\r\n"), "s.sym"));

        Assert.Equal("FIX vuoto", fix.Nome);
        // La base del triangolo è la riga 7, da un bordo all'altro; la punta è in alto al centro (x 6, y 1).
        Assert.All(Enumerable.Range(0, 13), x => Assert.True(fix.Acceso(x, 7)));
        Assert.True(fix.Acceso(6, 1));
        Assert.False(fix.Acceso(6, 0));
        Assert.False(fix.Acceso(0, 0));
    }

    [Fact]
    public void NumeriNomiERigheComeNelFile()
    {
        var simboli = Parser.Parse(ParserTestHelpers.Read(
            "//APT\r\n" + Apt + "\r\n\r\nAC_comb SEL\r\n" + Fix + "\r\n////ALTRI\r\n\r\n//CROCE X\r\n" + Fix + "\r\n\r\n" + Apt + "\r\n"),
            "s.sym");

        Assert.Equal([1, 2, 3, 4], simboli.Select(s => s.Numero));
        Assert.Equal(["APT", "AC_comb SEL", "CROCE X", null], simboli.Select(s => s.Nome));
        Assert.Equal([false, true, false, false], simboli.Select(s => s.NomeSenzaCommento));
        Assert.Equal([2, 5, 9, 11], simboli.Select(s => s.Riga));
        Assert.Equal(0, _warnings.Count);
    }

    [Fact]
    public void UnSimboloCheNonE13Per13AvvisaESiSalta()
    {
        var simboli = Parser.Parse(ParserTestHelpers.Read("//CORTO\r\n0000;1111;\r\n//APT\r\n" + Apt + "\r\n"), "s.sym");

        Assert.Equal(1, _warnings.Count);
        var apt = Assert.Single(simboli);
        Assert.Equal("APT", apt.Nome);
        Assert.Equal(1, apt.Numero);
    }
}
