namespace Vipi.Sectorfile.IO;

/// <summary>
/// Immutable snapshot of the dirty records for a single file, handed to the FileSaverOrchestrator
/// by the SessionService (Phase 13). Dirty membership is by reference identity.
/// </summary>
public sealed class DirtyFileInfo
{
    public DirtyFileInfo(string filePath, IEnumerable<object> dirtyRecords)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(dirtyRecords);

        FilePath = filePath;
        DirtyRecords = dirtyRecords.ToHashSet(ReferenceEqualityComparer.Instance);
    }

    public string FilePath { get; }

    /// <summary>Snapshot taken at construction; later session edits are not reflected here.</summary>
    public IReadOnlyCollection<object> DirtyRecords { get; }
}
