namespace Vipi.Sectorfile.IO;

/// <summary>Non-generic marker for any file saver (see <see cref="IFileParser"/> for the rationale).</summary>
public interface IFileSaver;

/// <summary>
/// Serialises domain objects back to their Aurora file format.
/// Implementations must be stateless and thread-safe.
/// </summary>
public interface IFileSaver<T> : IFileSaver
{
    /// <summary>
    /// Serialises a single record to its file lines. Does NOT include leading comments,
    /// //Start, or //End markers — those are added by <see cref="FileSaverOrchestrator"/>.
    /// Returns an ordered, non-empty list of complete file lines without trailing newline chars.
    /// </summary>
    IReadOnlyList<string> Serialize(T record);

    /// <summary>
    /// Returns the identifier used in //Start / //End markers for this record. Must be a
    /// non-null, non-empty, newline-free string, deterministic across saves of the same record,
    /// and unique within the file (the orchestrator appends _2, _3… on collisions).
    /// </summary>
    string GetIdentifier(T record);
}
