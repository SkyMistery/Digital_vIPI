using System.Drawing;
using Vipi.Sectorfile.IO;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Gli schemi di colori di Aurora (<c>.clr</c>), lotto «Subito» slice 4. Le righe sono quelle vere degli schemi del fork
/// (<c>ColorSchemes\</c>): la regola dei byte si prova con le coppie che nello stesso schema danno lo stesso colore.
/// </summary>
public sealed class ClrParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private ClrParser Parser => new(_warnings);

    private static int Argb(int r, int g, int b) => Color.FromArgb(0xFF, r, g, b).ToArgb();

    [Theory]
    // ITALY_GND.clr
    [InlineData("DANGER=$00963CAE", "SPEC_DANGER=$FFAE3C96")]
    [InlineData("PROHIBITED=$000064C6", "SPEC_PROHIBITED=$FFC66400")]
    // LIRR_RDR_V1.0.clr
    [InlineData("DANGER=$00F06E90", "SPEC_DANGER=$FF906EF0")]
    [InlineData("PROHIBITED=$00006DFF", "SPEC_PROHIBITED=$FFFF6D00")]
    [InlineData("ARTCC=$00F06E90", "COAST=$FF906EF0")]
    [InlineData("AIRPORT=$007D716D", "RUNWAY=$FF6D717D")]
    public void ByteAltoAZeroEBgrAltrimentiArgb_LeCoppieDanLoStessoColore(string bgr, string argb)
    {
        var schema = Parser.Parse(ParserTestHelpers.Read(bgr + "\r\n" + argb + "\r\n"), "prova.clr");

        string[] chiavi = [bgr.Split('=')[0], argb.Split('=')[0]];
        Assert.True(schema.TryColore(chiavi[0], out Color? primo));
        Assert.True(schema.TryColore(chiavi[1], out Color? secondo));
        Assert.Equal(primo!.Value.ToArgb(), secondo!.Value.ToArgb());
        Assert.Equal(0, _warnings.Count);
    }

    [Fact]
    public void Bgr_IlRossoStaNelByteBasso()
    {
        // ARTCC di LIRR_RDR_V1.0: $00F06E90 → rosso 90, verde 6E, blu F0 (un viola-blu).
        var schema = Parser.Parse(ParserTestHelpers.Read("ARTCC=$00F06E90\r\n"), "prova.clr");
        Assert.True(schema.TryColore("artcc", out Color? colore));
        Assert.Equal(Argb(0x90, 0x6E, 0xF0), colore!.Value.ToArgb());
    }

    [Fact]
    public void Argb_TieneLOpacita()
    {
        // AIRCRAFT_LBL_FIELD di LIRR_RDR_V1.0: l'unico valore del fork con un'opacità a metà.
        var schema = Parser.Parse(ParserTestHelpers.Read("AIRCRAFT_LBL_FIELD=$90525252\r\n"), "prova.clr");
        Assert.True(schema.TryColore("AIRCRAFT_LBL_FIELD", out Color? colore));
        Assert.Equal(Color.FromArgb(0x90, 0x52, 0x52, 0x52).ToArgb(), colore!.Value.ToArgb());
    }

    [Fact]
    public void NomiDiDelphi_EClNoneCheNonDisegna()
    {
        var schema = Parser.Parse(ParserTestHelpers.Read(
            "AIRCRAFTSELECTED=clWhite\r\nAIRCRAFTROUTE=clLime\r\nAIRCRAFT_COO_ACCEPT=clGreen\r\nATCPOSITION=clNone\r\n"),
            "prova.clr");

        Assert.True(schema.TryColore("AIRCRAFTSELECTED", out Color? bianco));
        Assert.Equal(Argb(0xFF, 0xFF, 0xFF), bianco!.Value.ToArgb());
        Assert.True(schema.TryColore("AIRCRAFTROUTE", out Color? lime));
        Assert.Equal(Argb(0x00, 0xFF, 0x00), lime!.Value.ToArgb());
        Assert.True(schema.TryColore("AIRCRAFT_COO_ACCEPT", out Color? verde));
        Assert.Equal(Argb(0x00, 0x80, 0x00), verde!.Value.ToArgb());

        Assert.True(schema.TryColore("ATCPOSITION", out Color? nessuno));
        Assert.Null(nessuno);
        Assert.Equal(0, _warnings.Count);
    }

    [Fact]
    public void LeImpostazioniNonSonoColori()
    {
        var schema = Parser.Parse(ParserTestHelpers.Read("ACC_HIGH_SOLID=3\r\nVORSYMBOL=«\r\nMRVA_SOLID=2\r\n"), "prova.clr");

        Assert.Empty(schema.Colori);
        Assert.Equal("3", schema.Altri["ACC_HIGH_SOLID"]);
        Assert.Equal("«", schema.Altri["VORSYMBOL"]);
        Assert.False(schema.TryColore("MRVA_SOLID", out _));
        Assert.Equal(0, _warnings.Count);
    }

    [Theory]
    [InlineData("COAST=$FF906E")]      // cifre in meno
    [InlineData("COAST=$FF906EFG")]    // non esadecimale
    [InlineData("COAST=clViola")]      // non è un nome di Delphi
    [InlineData("senza uguale")]
    public void UnaRigaStortaAvvisaESiSalta(string riga)
    {
        var schema = Parser.Parse(ParserTestHelpers.Read(riga + "\r\nARTCC=$00F06E90\r\n"), "prova.clr");

        Assert.Equal(1, _warnings.Count);
        Assert.Single(schema.Colori);
    }
}
