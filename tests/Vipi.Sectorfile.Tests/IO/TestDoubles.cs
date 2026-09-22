using System.Text;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>A minimal record for orchestrator/registry tests.</summary>
internal sealed class FakeRecord
{
    public FakeRecord(string id, params string[] body)
    {
        Id = id;
        Body = body;
    }

    public string Id { get; }
    public string[] Body { get; }
}

internal sealed class FakeSaver : IFileSaver<FakeRecord>
{
    public IReadOnlyList<string> Serialize(FakeRecord record) => record.Body;

    public string GetIdentifier(FakeRecord record) => record.Id;
}

internal sealed class FakeParser : IFileParser<FakeRecord>
{
    public ParseResult<FakeRecord> Parse(string filePath, ColorPalette palette)
        => new(Array.Empty<FakeRecord>(),
               new FileChunk<FakeRecord>[] { new RawChunk<FakeRecord>(Array.Empty<string>()) },
               Encoding.UTF8,
               HasByteOrderMark: false);
}

/// <summary>In-memory <see cref="IWarningCollector"/> that records every warning for assertions.</summary>
internal sealed class CollectingWarnings : IWarningCollector
{
    private readonly List<LoadWarning> _warnings = new();

    public void Add(LoadWarning warning)
    {
        _warnings.Add(warning);
        WarningAdded?.Invoke(this, warning);
    }

    public void Add(WarningSeverity severity, WarningCategory category, string source, string message,
                    int? lineNumber = null, string? rawSnippet = null)
        => Add(new LoadWarning(severity, category, source, message, lineNumber, rawSnippet));

    public IReadOnlyList<LoadWarning> Snapshot() => _warnings.ToArray();
    public event EventHandler<LoadWarning>? WarningAdded;
    public void Clear() => _warnings.Clear();
    public int Count => _warnings.Count;
}

/// <summary>
/// The real sector files the tests read, in <c>tests/Vipi.Sectorfile.Tests/Campioni</c> (copied from the
/// sector's <c>master</c>, see Campioni/LEGGIMI.md), found by walking up from the test assembly.
/// </summary>
/// <remarks>
/// ⚠️ Changed on the way into vIPI, on purpose. In library A the tree lived next to the code and was absent
/// in CI: <see cref="Path"/> returned null and ~30 tests returned early — green without checking anything.
/// Here a missing sample THROWS, so a test is either really run or red. The callers' <c>if (path is null)
/// return;</c> lines are A's and are now dead; left as they were to keep the port a plain copy.
/// </remarks>
internal static class RealSectorFiles
{
    public static string RootIt { get; } = Locate();

    public static string? Path(string relativeUnderIt)
    {
        string path = System.IO.Path.Combine(RootIt, relativeUnderIt.Replace('/', System.IO.Path.DirectorySeparatorChar));
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException($"Sample missing from Campioni/: {relativeUnderIt}", path);
    }

    private static string Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(dir.FullName, "Campioni");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Campioni folder above the test assembly.");
    }
}

/// <summary>Shared helpers for parser tests: build a FileReadResult from text, run a round-trip.</summary>
internal static class ParserTestHelpers
{
    public static FileReadResult Read(string text) => SectorFileReader.Decode(Encoding.UTF8.GetBytes(text));

    /// <summary>Parses a real file then saves it with an empty dirty set; returns (original, written) bytes.</summary>
    public static (byte[] original, byte[] written) RoundTrip<T>(
        IFileParser<T> parser, IFileSaver<T> saver, string realPath)
    {
        byte[] original = File.ReadAllBytes(realPath);
        var parseResult = parser.Parse(realPath, new ColorPalette());

        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "asd_rt_" + Guid.NewGuid().ToString("N") + ".sf");
        try
        {
            new FileSaverOrchestrator().Save(parseResult, new HashSet<T>(), saver, tmp);
            return (original, File.ReadAllBytes(tmp));
        }
        finally
        {
            if (File.Exists(tmp))
            {
                File.Delete(tmp);
            }
        }
    }
}
