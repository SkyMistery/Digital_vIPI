using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>RwParser / RwSaver — TEST_MATRIX §15.1 … §15.9.</summary>
public sealed class RwParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private RwParser Parser => new(_warnings);
    private ParseResult<Runway> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "itrw.rw");

    private const string Piste =
        "//PISTE\r\n" +
        "LIAA;09;27;113;113;095;275;N042.34.24.770;E012.34.54.460;N042.34.23.170;E012.35.19.600;\r\n";

    // §15.1 / §15.2 — round-trip the real .rw files (all three sections).
    [Fact]
    public void RoundTrip_Real_Itrw()
    {
        string? path = RealSectorFiles.Path("OTHER/itrw.rw");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new RwSaver(), path);
        Assert.Equal(o, w);
    }

    [Fact]
    public void RoundTrip_Real_Lirr()
    {
        string? path = RealSectorFiles.Path("OTHER/lirr.rw");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new RwSaver(), path);
        Assert.Equal(o, w);
    }

    // §15.3 / §15.5 — //MENU MAPPE and //ACC content goes to RawChunk, never a Runway.
    [Fact]
    public void Parse_MenuMappeAndAcc_RawChunk()
    {
        var pr = Parse(
            "//MENU MAPPE\r\n" +
            "LIAP;MAPS;;0;0;0;0;N000.00.00.000;E000.00.00.000;N000.00.00.000;E000.00.00.000;\r\n" +
            Piste +
            "//ACC\r\nsome legacy acc line\r\n");

        Assert.Single(pr.Records);                       // only the //PISTE runway
        Assert.Equal("LIAA", pr.Records[0].IcaoCode);
        Assert.Contains(pr.Chunks, c => c is RawChunk<Runway> raw && raw.Lines.Any(l => l.StartsWith("LIAP;MAPS")));
        Assert.Contains(pr.Chunks, c => c is RawChunk<Runway> raw && raw.Lines.Contains("some legacy acc line"));
    }

    // §15.4 — each //PISTE line → one Runway with correct fields.
    [Fact]
    public void Parse_Piste_OneRunwayPerLine()
    {
        var pr = Parse(Piste + "LIAF;17;35;730;696;170;350;N042.56.22.000;E012.42.39.000;N042.55.38.000;E012.42.48.000;\r\n");
        Assert.Equal(2, pr.Records.Count);
        var r = pr.Records[0];
        Assert.Equal("LIAA", r.IcaoCode);
        Assert.Equal("09", r.Designator1);
        Assert.Equal("27", r.Designator2);
        Assert.Equal(113, r.ElevThresh1Ft);
        Assert.Equal(95f, r.TrueHeading1);
        Assert.Equal(275f, r.TrueHeading2);
    }

    // §15.6 — empty TrueHeading2 → null.
    [Fact]
    public void Parse_EmptyHeading2_Null()
    {
        var r = Parse("//PISTE\r\nLIMW;27;09;1774;1796;261;;N045.44.20.990;E007.22.37.750;N045.44.16.610;E007.21.28.700;\r\n").Records[0];
        Assert.Null(r.TrueHeading2);
        Assert.Equal(261f, r.TrueHeading1);
    }

    // §15.7 — file with only //MENU MAPPE and //ACC → no records.
    [Fact]
    public void Parse_NoPisteSection_NoRecords()
    {
        var pr = Parse("//MENU MAPPE\r\nLIAP;MAPS;;0;0;0;0;N000.00.00.000;E000.00.00.000;N000.00.00.000;E000.00.00.000;\r\n//ACC\r\nx\r\n");
        Assert.Empty(pr.Records);
    }

    // §15.8 — designators like 16L/34R preserved.
    [Fact]
    public void Parse_Designators_Preserved()
    {
        var r = Parse("//PISTE\r\nLIBA;11L;29R;178;182;109.5;289.5;N041.32.39.760;E015.41.57.420;N041.32.05.070;E015.43.42.470;\r\n").Records[0];
        Assert.Equal("11L", r.Designator1);
        Assert.Equal("29R", r.Designator2);
        Assert.Equal(109.5f, r.TrueHeading1);
    }

    /// <summary>
    /// 🔴 Trovato in F3 (slice 3b), disegnando le piste sulla mappa: i quattro .rw di FIR scrivono l'intestazione con
    /// sette barre (<c>///////PISTE</c>), e A guardava solo <c>//PISTE</c>. Le loro 187 righe di pista restavano righe
    /// grezze: round-trip perfetto, piste invisibili al modello, nessun avviso. Sull'albero «una modifica per record»
    /// è salita da 115 382 a 115 569, cioè esattamente quelle righe.
    /// </summary>
    [Fact]
    public void Parse_IntestazioneConPiuBarre_LeggeLePiste()
    {
        var pr = Parse("///////PISTE\r\nLIAA;09;27;113;113;095;275;N042.34.24.770;E012.34.54.460;N042.34.23.170;E012.35.19.600;\r\n");

        Assert.Single(pr.Records);
        Assert.Equal("LIAA", pr.Records[0].IcaoCode);
    }

    /// <summary>E il file vero coi sette slash: prima zero record, ora le sue piste.</summary>
    [Fact]
    public void Parse_LirrRw_LeggeLePisteDelFirDiRoma()
    {
        string? path = RealSectorFiles.Path("OTHER/lirr.rw");
        if (path is null)
        {
            return;
        }

        var pr = Parser.Parse(path, new ColorPalette());

        Assert.NotEmpty(pr.Records);
        Assert.Contains(pr.Records, r => r.IcaoCode == "LIRF" && r.Designator1 == "16L");
    }

    // §15.9 — malformed line inside //PISTE → RawChunk + warning.
    [Fact]
    public void Parse_MalformedPisteLine_RawChunkAndWarning()
    {
        var pr = Parse("//PISTE\r\nLIAA;09;27;113;\r\n");
        Assert.Empty(pr.Records);
        Assert.Equal(1, _warnings.Count);
        Assert.Contains(pr.Chunks, c => c is RawChunk<Runway>);
    }
}
