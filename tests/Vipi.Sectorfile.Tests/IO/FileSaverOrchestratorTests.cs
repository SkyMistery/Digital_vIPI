using System.Text;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>FileSaverOrchestrator coverage — DEVELOPMENT_PLAN §26.1 … §26.9 and §33.4.</summary>
public sealed class FileSaverOrchestratorTests : IDisposable
{
    private readonly FileSaverOrchestrator _orchestrator = new();
    private readonly FakeSaver _saver = new();
    private readonly List<string> _tempPaths = new();

    // §26.1 — empty dirty set → byte-for-byte identical output (NFR-04).
    [Fact]
    public void EmptyDirtySet_ProducesByteForByteCopy()
    {
        byte[] original = Encoding.UTF8.GetBytes("//header\r\nLINE1\r\nLINE2\r\n");
        string path = WriteTemp(original);

        var read = SectorFileReader.Decode(original);
        var parseResult = FromLines(read.Lines, read.NewLine, read.HasFinalNewLine);

        _orchestrator.Save(parseResult, Dirty(), _saver, path);

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    // §26.2 — REVERSED in vIPI (F2 slice 2, carta madre §8): a dirty record gets its new body and NO
    // //Start / //End markers. In A it gained them: the file of the sector would have filled up with them.
    [Fact]
    public void DirtyRecord_WithoutMarkers_StaysWithoutMarkers()
    {
        var rec = new FakeRecord("ID1", "NEW");
        var pr = WithChunks(new[] { rec }, new RecordChunk<FakeRecord>(rec, new[] { "OLD" }, hasMarkers: false));

        var lines = SaveAndReadLines(pr, Dirty(rec));

        Assert.Equal(new[] { "NEW" }, lines);
    }

    // §26.3 — dirty record that already had markers → markers preserved, not duplicated.
    [Fact]
    public void DirtyRecord_WithMarkers_NotDuplicated()
    {
        var rec = new FakeRecord("ID1", "NEW");
        var pr = WithChunks(new[] { rec }, new RecordChunk<FakeRecord>(rec, new[] { "OLD" }, hasMarkers: true));

        var lines = SaveAndReadLines(pr, Dirty(rec));

        Assert.Equal(new[] { "//Start ID1", "NEW", "//End ID1" }, lines);
        Assert.Single(lines, l => l == "//Start ID1");
    }

    // §26.4 — non-dirty record with markers → markers kept, RawLines verbatim.
    [Fact]
    public void NonDirtyRecord_WithMarkers_Kept()
    {
        var rec = new FakeRecord("ID1", "NEW");
        var pr = WithChunks(new[] { rec }, new RecordChunk<FakeRecord>(rec, new[] { "OLD" }, hasMarkers: true));

        var lines = SaveAndReadLines(pr, Dirty());

        Assert.Equal(new[] { "//Start ID1", "OLD", "//End ID1" }, lines);
    }

    // §26.5 — non-dirty record without markers → no markers added.
    [Fact]
    public void NonDirtyRecord_WithoutMarkers_NoMarkers()
    {
        var rec = new FakeRecord("ID1", "NEW");
        var pr = WithChunks(new[] { rec }, new RecordChunk<FakeRecord>(rec, new[] { "OLD" }, hasMarkers: false));

        var lines = SaveAndReadLines(pr, Dirty());

        Assert.Equal(new[] { "OLD" }, lines);
    }

    // §26.6 — LeadingComments written before the record (and before //Start, where a record already has it).
    [Fact]
    public void LeadingComments_WrittenBeforeTheRecord()
    {
        var rec = new FakeRecord("ID1", "NEW");
        var chunk = new RecordChunk<FakeRecord>(rec, new[] { "OLD" }, hasMarkers: true, leadingComments: new[] { "// hi", "// there" });
        var pr = WithChunks(new[] { rec }, chunk);

        var lines = SaveAndReadLines(pr, Dirty(rec));

        Assert.Equal(new[] { "// hi", "// there", "//Start ID1", "NEW", "//End ID1" }, lines);
    }

    // §26.7 — RawChunk lines written verbatim, in order.
    [Fact]
    public void RawChunk_WrittenVerbatim()
    {
        var pr = WithChunks(Array.Empty<FakeRecord>(), new RawChunk<FakeRecord>(new[] { "a", "b", "c" }));

        var lines = SaveAndReadLines(pr, Dirty());

        Assert.Equal(new[] { "a", "b", "c" }, lines);
    }

    // §26.8 — IOException during write is propagated.
    [Fact]
    public void WriteFailure_PropagatesIOException()
    {
        var pr = WithChunks(Array.Empty<FakeRecord>(), new RawChunk<FakeRecord>(new[] { "x" }));
        string badPath = Path.Combine(Path.GetTempPath(), "asd_nonexistent_" + Guid.NewGuid().ToString("N"), "f.txt");

        Assert.Throws<DirectoryNotFoundException>(() => _orchestrator.Save(pr, Dirty(), _saver, badPath));
    }

    // §26.9 — duplicate identifiers → second record gets the _2 suffix.
    [Fact]
    public void DuplicateIdentifiers_GetSuffix()
    {
        var r1 = new FakeRecord("VCO", "L1");
        var r2 = new FakeRecord("VCO", "L2");
        var pr = WithChunks(
            new[] { r1, r2 },
            new RecordChunk<FakeRecord>(r1, new[] { "L1" }, hasMarkers: true),
            new RecordChunk<FakeRecord>(r2, new[] { "L2" }, hasMarkers: true));

        var lines = SaveAndReadLines(pr, Dirty());

        Assert.Contains("//Start VCO", lines);
        Assert.Contains("//Start VCO_2", lines);
        Assert.Contains("//End VCO_2", lines);
    }

    // §33.4 — round-trip preserves encoding and BOM byte-for-byte.
    [Fact]
    public void RoundTrip_PreservesBomAndEncoding()
    {
        byte[] bom = { 0xEF, 0xBB, 0xBF };
        byte[] original = bom.Concat(Encoding.UTF8.GetBytes("à\r\nb\r\n")).ToArray();
        string path = WriteTemp(original);

        var read = SectorFileReader.Decode(original);
        Assert.True(read.HasByteOrderMark);
        var pr = FromLines(read.Lines, read.NewLine, read.HasFinalNewLine, read.Encoding, read.HasByteOrderMark);

        _orchestrator.Save(pr, Dirty(), _saver, path);

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    // §26.1 (edge) — a file with mixed newlines round-trips byte-for-byte on an empty dirty set.
    // The dominant terminator (CRLF) is the split/join newline; the minority LF survives embedded
    // in line content, so the join reproduces the original exactly (NFR-04). Regression lock.
    [Fact]
    public void RoundTrip_MixedNewlines_ByteForByte()
    {
        byte[] original = Encoding.UTF8.GetBytes("a\r\nb\nc\r\n");
        string path = WriteTemp(original);

        var read = SectorFileReader.Decode(original);
        var pr = FromLines(read.Lines, read.NewLine, read.HasFinalNewLine, read.Encoding, read.HasByteOrderMark);

        _orchestrator.Save(pr, Dirty(), _saver, path);

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static ISet<FakeRecord> Dirty(params FakeRecord[] records) => new HashSet<FakeRecord>(records);

    private static ParseResult<FakeRecord> WithChunks(IReadOnlyList<FakeRecord> records, params FileChunk<FakeRecord>[] chunks)
        => new(records, chunks, new UTF8Encoding(false), HasByteOrderMark: false);

    private static ParseResult<FakeRecord> FromLines(
        IReadOnlyList<string> lines, string newLine, bool hasFinalNewLine,
        Encoding? encoding = null, bool hasBom = false)
        => new(Array.Empty<FakeRecord>(),
               new FileChunk<FakeRecord>[] { new RawChunk<FakeRecord>(lines) },
               encoding ?? new UTF8Encoding(false),
               hasBom)
        {
            NewLine = newLine,
            HasFinalNewLine = hasFinalNewLine,
        };

    private string[] SaveAndReadLines(ParseResult<FakeRecord> parseResult, ISet<FakeRecord> dirty)
    {
        string path = NewTempPath();
        _orchestrator.Save(parseResult, dirty, _saver, path);
        return SectorFileReader.Decode(File.ReadAllBytes(path)).Lines.ToArray();
    }

    private string WriteTemp(byte[] bytes)
    {
        string path = NewTempPath();
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string NewTempPath()
    {
        string path = Path.Combine(Path.GetTempPath(), "asd_io_" + Guid.NewGuid().ToString("N") + ".sf");
        _tempPaths.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // best-effort temp cleanup
            }
        }
    }
}
