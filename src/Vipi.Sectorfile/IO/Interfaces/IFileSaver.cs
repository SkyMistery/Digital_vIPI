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
    /// Serialises a single record to its file lines, points in dotted DMS. Does NOT include leading
    /// comments or markers; <see cref="FileSaverOrchestrator"/> writes the comments back and turns the
    /// points into the form the record was written in.
    /// Returns an ordered, non-empty list of complete file lines without trailing newline chars.
    /// </summary>
    IReadOnlyList<string> Serialize(T record);

    /// <summary>
    /// Returns the identifier used in the //Start / //End markers of a record that already has them
    /// (the orchestrator no longer adds any, F2 slice 2). Must be a
    /// non-null, non-empty, newline-free string, deterministic across saves of the same record,
    /// and unique within the file (the orchestrator appends _2, _3… on collisions).
    /// </summary>
    string GetIdentifier(T record);
}
