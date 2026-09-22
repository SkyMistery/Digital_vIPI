using System.Text;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.Models.Tests;

/// <summary>
/// Phase 2 structural smoke tests (DEVELOPMENT_PLAN M-2.1 … M-2.8). These assert the shape of
/// the domain model — no business logic lives in the model classes.
/// </summary>
public class ModelsSmokeTests
{
    // M-2.1 — ParseResult exposes Records and distinct Chunks.
    [Fact]
    public void ParseResult_HoldsRecordsAndDistinctChunks()
    {
        var c1 = new RecordChunk<string>("r1", new[] { "r1-line" }, hasMarkers: false);
        var c2 = new RecordChunk<string>("r2", new[] { "r2-line" }, hasMarkers: false);
        var result = new ParseResult<string>(
            new[] { "r1", "r2" },
            new FileChunk<string>[] { c1, c2 },
            Encoding.UTF8,
            HasByteOrderMark: false);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(2, result.Chunks.Count);
        Assert.NotSame(result.Chunks[0], result.Chunks[1]);
    }

    // M-2.2 — RecordChunk and RawChunk are correctly-typed members of the discriminated union.
    [Fact]
    public void FileChunk_DiscriminatedUnion_TypesAreCorrect()
    {
        var record = new RecordChunk<string>(
            "rec",
            new[] { "raw1", "raw2" },
            hasMarkers: true,
            leadingComments: new[] { "// hello" });
        var raw = new RawChunk<string>(new[] { "blank", "// comment" });

        Assert.IsType<RecordChunk<string>>(record);
        Assert.IsType<RawChunk<string>>(raw);
        Assert.IsAssignableFrom<FileChunk<string>>(record);
        Assert.IsAssignableFrom<FileChunk<string>>(raw);

        Assert.Equal("rec", record.Record);
        Assert.True(record.HasMarkers);
        Assert.Equal(new[] { "// hello" }, record.LeadingComments);
        Assert.Equal(new[] { "raw1", "raw2" }, record.RawLines);
        Assert.Equal(new[] { "blank", "// comment" }, raw.Lines);
    }

    // M-2.3 — SectorPackage *Ready Tasks start incomplete.
    [Fact]
    public void SectorPackage_ReadySignals_StartIncomplete()
    {
        var package = new SectorPackage("C:/sf/ITALY.isc");

        Assert.False(package.NavaidsReady.IsCompleted);
        Assert.False(package.AtzShapesReady.IsCompleted);
        Assert.False(package.FrequenciesReady.IsCompleted);
        Assert.False(package.FicAirspaceReady.IsCompleted);

        package.CompleteNavaids();
        Assert.True(package.NavaidsReady.IsCompletedSuccessfully);
    }

    // M-2.4 — NavaidSet lookup: added navaid found by ident; missing ident → false.
    [Fact]
    public void NavaidSet_Lookup_ByIdent()
    {
        var navaids = new NavaidSet();
        var pos = new Coordinate(41.8, 12.5);
        navaids.AddVor(new Vor { Ident = "ROM", Position = pos });

        Assert.True(navaids.TryResolve("ROM", out var found));
        Assert.Equal(pos, found);
        Assert.False(navaids.TryResolve("XXX", out _));
        Assert.Single(navaids.Vors);
    }

    // M-2.5 — Catalog empty initially; adding a FirDescriptor → Firs.Count == 1.
    [Fact]
    public void Catalog_AddFir_IncrementsFirs()
    {
        var catalog = new Catalog();
        Assert.Empty(catalog.Firs);
        Assert.Empty(catalog.Airports);

        catalog.AddFir(new FirDescriptor { FirCode = "LIRR", Name = "Roma ACC" });
        Assert.Single(catalog.Firs);
    }

    // M-2.6 — TflSector.Type inferred: SectorCode ending "FSS" → Fss.
    [Fact]
    public void TflSector_SectorType_InferredAsFss()
    {
        var fss = new TflSector { SectorCode = "LIMM_FSS", FillColor = "CTR" };
        Assert.Equal(SectorType.Fss, fss.Type);

        var ctr = new TflSector { SectorCode = "LIRR_NE_CTR", FillColor = "CTR" };
        Assert.Equal(SectorType.Ctr, ctr.Type);
    }

    // M-2.7 — FicSector is a TflSector.
    [Fact]
    public void FicSector_IsTflSector()
    {
        var fic = new FicSector { SectorCode = "LIMM_FSS", FillColor = "CTR" };
        Assert.IsAssignableFrom<TflSector>(fic);
        Assert.True(fic.IsFssPerimeter);

        var geo = new FicSector { SectorCode = "GARDA", FillColor = "LIMMFIC", ShapeLabel = "GARDA" };
        Assert.False(geo.IsFssPerimeter);
    }

    // M-2.8 — ParseResult preserves HasByteOrderMark after a record-style clone.
    [Fact]
    public void ParseResult_Clone_PreservesByteOrderMark()
    {
        var original = new ParseResult<string>(
            new[] { "r1" },
            Array.Empty<FileChunk<string>>(),
            Encoding.UTF8,
            HasByteOrderMark: true);

        var clone = original with { };

        Assert.True(clone.HasByteOrderMark);
        Assert.Equal(original.Encoding, clone.Encoding);
        Assert.Equal(original.Records, clone.Records);
    }
}
