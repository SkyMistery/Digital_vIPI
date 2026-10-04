using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>TflParser / TflSaver — TEST_MATRIX §5.1 … §5.12.</summary>
public sealed class TflParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private TflParser Parser => new(_warnings);
    private ParseResult<TflSector> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lirrctr.tfl");

    private const string V = "N041.00.00.000;E012.00.00.000;\r\nN041.10.00.000;E012.10.00.000;\r\nN041.20.00.000;E012.20.00.000;\r\n";

    // §5.1 / §5.2 / §5.3 — round-trip the real .tfl files.
    [Theory]
    [InlineData("DYNAMIC_SEC/lirrctr.tfl")]
    [InlineData("OTHER/GCI.tfl")]
    [InlineData("DYNAMIC_SEC/twrs.tfl")]
    public void RoundTrip_Real(string rel)
    {
        string? path = RealSectorFiles.Path(rel);
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new TflSaver(), path);
        Assert.Equal(o, w);
    }

    // §5.4 — SectorCode ending "FSS" → Fss regardless of FillColor.
    [Fact]
    public void SectorCode_EndsFss_IsFss()
        => Assert.Equal(SectorType.Fss, Parse("LIRR_FSS;CTR;1;CTR;1;\r\n" + V).Records[0].Type);

    // §5.5 — LIRR_NE_CTR + FillColor CTR → Ctr (not FSS).
    [Fact]
    public void Ctr_NotFss()
        => Assert.Equal(SectorType.Ctr, Parse("LIRR_NE_CTR;CTR;1;CTR;1;\r\n" + V).Records[0].Type);

    // §5.6 — hex FillColor accepted and preserved verbatim.
    [Fact]
    public void HexFillColor_Verbatim()
    {
        var r = Parse("LIRE_APP;#0C0C0C;1;#0C0C0C;1;\r\n" + V).Records[0];
        Assert.Equal("#0C0C0C", r.FillColor);
        Assert.Equal("#0C0C0C", r.StrokeColor);
    }

    // §5.7 — palette-name FillColor accepted.
    [Fact]
    public void PaletteFillColor()
    {
        var r = Parse("LIRE_APP;APP;1;APP;1;\r\n" + V).Records[0];
        Assert.Equal("APP", r.FillColor);
        Assert.Equal(SectorType.App, r.Type);
    }

    // §5.8 — colon-separated multi-position SectorCode kept as an opaque string.
    [Fact]
    public void ColonSeparatedCode_Opaque()
        => Assert.Equal("LIZZ_AEW_CTR:LIRO_CRC_CTR", Parse("LIZZ_AEW_CTR:LIRO_CRC_CTR;GCI;1;GCI;0;\r\n" + V).Records[0].SectorCode);

    // §5.9 — multiple sectors separated by a blank line.
    [Fact]
    public void MultipleSectors_Count()
        => Assert.Equal(2, Parse("A_APP;APP;1;APP;1;\r\n" + V + "\r\nB_APP;APP;1;APP;1;\r\n" + V).Records.Count);

    // §5.10 — a header with no vertices → empty Vertices.
    [Fact]
    public void ZeroVertices()
        => Assert.Empty(Parse("A_APP;APP;1;APP;1;\r\n\r\nB_APP;APP;1;APP;1;\r\n" + V).Records[0].Vertices);

    // §5.11 — SectorType inference for every enum value.
    [Theory]
    [InlineData("X_CTR", "CTR", SectorType.Ctr)]
    [InlineData("X", "APP", SectorType.App)]
    [InlineData("X_FSS", "CTR", SectorType.Fss)]
    [InlineData("X", "TMA", SectorType.Tma)]
    [InlineData("X", "UIR", SectorType.Uir)]
    [InlineData("X", "ATZ", SectorType.Atz)]
    [InlineData("X", "GCA", SectorType.Gca)]
    public void SectorTypeInference(string code, string fill, SectorType expected)
        => Assert.Equal(expected, Parse($"{code};{fill};1;{fill};1;\r\n" + V).Records[0].Type);

    // §5.12 — comments between sectors round-trip verbatim.
    [Fact]
    public void CommentsBetweenSectors_RoundTrip()
    {
        string text = "//ROMA\r\nA_APP;APP;1;APP;1;\r\n" + V + "\r\n//section\r\nB_APP;APP;1;APP;1;\r\n" + V;
        var pr = Parser.Parse(ParserTestHelpers.Read(text), "lirrctr.tfl");
        string tmp = Path.Combine(Path.GetTempPath(), "asd_tfl_" + Guid.NewGuid().ToString("N") + ".tfl");
        try
        {
            new FileSaverOrchestrator().Save(pr, new HashSet<TflSector>(), new TflSaver(), tmp);
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(text), File.ReadAllBytes(tmp));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    // Lotto «Subito» slice 13a — l'opacità è facoltativa (manuale IVAO, [FILLCOLOR]): una testa a quattro campi apre un
    // settore. Prima si leggeva come un vertice per nome, e le 52 teste così di GCI.tfl finivano nel primo settore.
    [Fact]
    public void TestaSenzaOpacita_ApreUnSettore()
    {
        var letti = Parse("A_CTR:B_CTR;GCI;1;GCI;0;\r\n" + V + "\r\nA_CTR:B_CTR;GCI;1;GCI;\r\n" + V).Records;

        Assert.Equal(2, letti.Count);
        Assert.Equal(3, letti[0].Vertices.Count);
        Assert.Equal(3, letti[1].Vertices.Count);
        Assert.False(letti[0].SenzaOpacita);
        Assert.True(letti[1].SenzaOpacita);
        Assert.Equal(0, _warnings.Count);
    }

    // La testa senza opacità si riscrive com'era; scritta l'opacità o il filtro, il campo compare.
    [Fact]
    public void TestaSenzaOpacita_SiRiscriveComEra()
    {
        var settore = Parse("A_CTR;GCI;1;GCI;\r\n" + V).Records[0];
        Assert.Equal("A_CTR;GCI;1;GCI;", new TflSaver().Serialize(settore)[0]);

        settore.Flags = 1;
        Assert.Equal("A_CTR;GCI;1;GCI;1;", new TflSaver().Serialize(settore)[0]);

        settore.Flags = 0;
        settore.Filtro = "TAXIWAY";
        Assert.Equal("A_CTR;GCI;1;GCI;0;TAXIWAY;", new TflSaver().Serialize(settore)[0]);
    }

    // Il 6° campo è il filtro (COAST, RUNWAY, GATES, PIER, TAXIWAY, APRON, BUILDING); un commento in coda non lo è.
    [Fact]
    public void Filtro_Letto_E_Scritto()
    {
        var conFiltro = Parse("Static;#00404040;1;#00404040;1;TAXIWAY;\r\n" + V).Records[0];
        Assert.Equal("TAXIWAY", conFiltro.Filtro);
        Assert.Equal("Static;#00404040;1;#00404040;1;TAXIWAY;", new TflSaver().Serialize(conFiltro)[0]);

        var conCommento = Parse("LIRR_NE_CTR;CTR;1;CTR;1; //NE cnf.1\r\n" + V).Records[0];
        Assert.Null(conCommento.Filtro);
        Assert.Equal("LIRR_NE_CTR;CTR;1;CTR;1;", new TflSaver().Serialize(conCommento)[0]);
    }

    // Le posizioni della testa: separate da spazio (manuale) o da due punti (GCI.tfl, i confini di limmctr.tfl).
    [Theory]
    [InlineData("LGAV_APP LGAV_DEP", "LGAV_APP,LGAV_DEP")]
    [InlineData("LIZZ_AEW_CTR:LIRO_CRC_CTR:LIVK_CRC_CTR", "LIZZ_AEW_CTR,LIRO_CRC_CTR,LIVK_CRC_CTR")]
    [InlineData("LIBN_APP ", "LIBN_APP")]
    [InlineData("Static", "")]
    public void Posizioni_DellaTesta(string testa, string attese)
    {
        var settore = new TflSector { SectorCode = testa };
        Assert.Equal(attese, string.Join(",", settore.Posizioni()));
        Assert.Equal(attese.Length == 0, settore.Statico);
    }

    // Un vertice per nome resta un vertice (due campi), anche dopo una testa a quattro campi.
    [Fact]
    public void VerticePerNome_DopoUnaTestaCorta()
    {
        var settore = Parse("A_CTR;GCI;1;GCI;\r\nAMSOR;AMSOR;\r\n" + V).Records[0];
        Assert.Equal(4, settore.Vertices.Count);
        Assert.True(settore.Vertices[0].PerNome);
    }

    // GCI.tfl: 53 poligoni (la penisola e le isole), non uno.
    [Fact]
    public void Gci_Vero_HaTuttiISuoiSettori()
    {
        string? path = RealSectorFiles.Path("OTHER/GCI.tfl");
        if (path is null) return;
        var letti = Parser.Parse(path, new ColorPalette()).Records;
        Assert.Equal(File.ReadLines(path).Count(r => r.Contains(";GCI;1;GCI;", StringComparison.Ordinal)), letti.Count);
        Assert.DoesNotContain(letti, s => s.Vertices.Any(v => v.PerNome));
    }
}
