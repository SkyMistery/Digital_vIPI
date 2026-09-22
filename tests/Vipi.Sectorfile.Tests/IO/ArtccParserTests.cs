using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>ArtccParser / ArtccSaver — TEST_MATRIX §19 (ACC/*.artcc label files).</summary>
public sealed class ArtccParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private ParseResult<ElementoArtcc> ParseAll(string text)
        => new ArtccParser(_warnings).Parse(ParserTestHelpers.Read(text), "FRA.artcc", new Shared.ColorPalette());

    // The labels only: the .artcc record is a union since F2 slice 5 (labels and boundary groups).
    private List<LabelPoint> Labels(string text) => ParseAll(text).Records.OfType<LabelPoint>().ToList();

    // §19.1 — round-trip the real ACC files (lirr.artcc is absent; FRA*.artcc stand in).
    [Theory]
    [InlineData("ACC/FRA.artcc")]
    [InlineData("ACC/FRA-gates.artcc")]
    public void RoundTrip_Real(string rel)
    {
        string? path = RealSectorFiles.Path(rel);
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new ArtccParser(_warnings), new ArtccSaver(), path);
        Assert.Equal(o, w);
    }

    // §19.2 — a standard L; line → LabelPoint with Mode = FixName.
    [Fact]
    public void StandardLabel_FixNameMode()
    {
        var r = Labels("L;ABDAB;N037.53.21.000;E010.37.43.000;8;\r\n")[0];
        Assert.Equal(LabelMode.FixName, r.Mode);
        Assert.Equal("ABDAB", r.FixRef);
    }

    // §19.3 — FontSize preserved.
    [Fact]
    public void FontSize_Preserved()
        => Assert.Equal(8, Labels("L;ABDAB;N037.53.21.000;E010.37.43.000;8;\r\n")[0].FontSize);

    // §19.4 — ⚠️ OVERTURNED by F2 slice 5. In A a T; line was not a record (warning + RawChunk): those were 5 041
    // lines, every boundary of FRA.artcc and FRA-gates.artcc. Now a run of T; lines is a boundary group, as in
    // .hartcc: DUMMY separates its polygons, and no warning.
    [Fact]
    public void TLines_AreABoundaryGroup()
    {
        var pr = ParseAll(
            "L;ABDAB;N037.53.21.000;E010.37.43.000;8;\r\n" +
            "T;FRA BDRY;N043.10.00.000;E009.45.00.000;\r\n" +
            "T;FRA BDRY;N041.20.00.000;E009.45.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n" +
            "T;FRA BDRY;N041.00.00.000;E008.00.00.000;\r\n" +
            "L;ABESI;N046.09.35.000;E009.02.34.000;8;\r\n");

        Assert.Empty(_warnings.Snapshot());
        Assert.Collection(
            pr.Records,
            r => Assert.IsType<LabelPoint>(r),
            r =>
            {
                var group = Assert.IsType<StaticBoundaryGroup>(r);
                Assert.Equal("FRA BDRY", group.Name);
                Assert.Equal(new[] { 2, 1 }, group.Polygons.Select(p => p.Vertices.Count));
            },
            r => Assert.IsType<LabelPoint>(r));
    }

    // FRA.artcc: a run that OPENS with a DUMMY line took «DUMMY» as its name, and the saver wrote every vertex
    // back as a DUMMY line. The name is that of the first real vertex.
    [Fact]
    public void ARunOpeningWithDummy_TakesTheNameOfItsVertices()
    {
        var group = Assert.IsType<StaticBoundaryGroup>(Assert.Single(ParseAll(
            "T;DUMMY;N038.34.23.551;E008.08.07.652;\r\n" +
            "T;LIMITROFI;N036.37.12.000;E011.30.00.000;//confine tunis-malta\r\n" +
            "T;LIMITROFI;N036.14.29.087;E011.29.29.924;\r\n").Records));

        Assert.Equal("LIMITROFI", group.Name);
        Assert.All(new ArtccSaver().Serialize(group), l => Assert.StartsWith("T;LIMITROFI;", l));
    }

    // §19.5 — LabelMode.Custom → Serialize emits CustomName as field 2.
    [Fact]
    public void Custom_SerializesCustomName()
    {
        var label = new LabelPoint
        {
            Mode = LabelMode.Custom,
            CustomName = "MYLABEL",
            Position = new Shared.Coordinate(41, 12),
            FontSize = 8,
        };
        Assert.StartsWith("L;MYLABEL;", new ArtccSaver().Serialize(label)[0]);
    }

    // §19.6 — LabelMode.None → Serialize emits an empty field 2.
    [Fact]
    public void None_SerializesEmptyField()
    {
        var label = new LabelPoint
        {
            Mode = LabelMode.None,
            Position = new Shared.Coordinate(41, 12),
            FontSize = 8,
        };
        Assert.StartsWith("L;;", new ArtccSaver().Serialize(label)[0]);
    }

    // §19.7 — re-parsing our own output with an empty field 2 → LabelMode.None.
    [Fact]
    public void ReparseEmptyField_IsNone()
    {
        var label = new LabelPoint
        {
            Mode = LabelMode.None,
            Position = new Shared.Coordinate(41, 12),
            FontSize = 8,
        };
        string line = new ArtccSaver().Serialize(label)[0];
        var reparsed = Labels(line + "\r\n")[0];
        Assert.Equal(LabelMode.None, reparsed.Mode);
        Assert.Null(reparsed.FixRef);
    }
}
