namespace Vipi.Sectorfile.IO;

public enum WarningSeverity { Info, Warning, Error }

public enum WarningCategory
{
    Parser,       // malformed line skipped, unparseable coordinate, etc.
    FileMissing,  // F; reference not found on disk
    Metafile,     // schemaVersion mismatch, corrupted .asdx
    Conflict,     // divergent duplicate detected (informational)
    GitHub,       // pull/push/PR-related warnings
    Io,           // recoverable IO issue
    Other,
}

/// <summary>
/// A non-fatal anomaly raised during loading, parsing, saving or GitHub operations.
/// Produced by parsers (and others); collected by <see cref="IWarningCollector"/>.
/// </summary>
public sealed class LoadWarning
{
    public LoadWarning(
        WarningSeverity severity,
        WarningCategory category,
        string source,
        string message,
        int? lineNumber = null,
        string? rawSnippet = null)
    {
        Timestamp = DateTime.UtcNow;
        Severity = severity;
        Category = category;
        Source = source;
        Message = message;
        LineNumber = lineNumber;
        RawSnippet = Truncate(rawSnippet);
    }

    public DateTime Timestamp { get; }
    public WarningSeverity Severity { get; }
    public WarningCategory Category { get; }
    public string Source { get; }        // file path or service name
    public int? LineNumber { get; }      // 1-based, when applicable
    public string Message { get; }
    public string? RawSnippet { get; }   // first ≤120 chars of the offending line, if any

    /// <summary>Max length of <see cref="RawSnippet"/>; the collector also enforces this.</summary>
    public const int MaxSnippetLength = 120;

    private static string? Truncate(string? snippet)
        => snippet is { Length: > MaxSnippetLength } ? snippet[..MaxSnippetLength] : snippet;
}

/// <summary>
/// Thread-safe, append-only sink for non-blocking warnings. Defined in IO so parsers/savers can
/// depend on it via constructor injection; the concrete <c>WarningCollector</c> is implemented in
/// the Services layer (Phase 13). Registered as a singleton.
/// </summary>
public interface IWarningCollector
{
    /// <summary>Adds a warning. Returns synchronously.</summary>
    void Add(LoadWarning warning);

    /// <summary>Convenience overload — builds a <see cref="LoadWarning"/> internally.</summary>
    void Add(WarningSeverity severity, WarningCategory category,
             string source, string message,
             int? lineNumber = null, string? rawSnippet = null);

    /// <summary>All warnings collected so far, in insertion order, as an immutable snapshot.</summary>
    IReadOnlyList<LoadWarning> Snapshot();

    /// <summary>Raised synchronously inside <see cref="Add(LoadWarning)"/>, on the calling thread.</summary>
    event EventHandler<LoadWarning> WarningAdded;

    /// <summary>Removes all collected warnings. Typically called on Reset.</summary>
    void Clear();

    /// <summary>Number of warnings currently collected.</summary>
    int Count { get; }
}
