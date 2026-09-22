namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Tracks the origin of a data field in the file system.
/// Multiple entries for one logical record = data duplicated across files.
/// </summary>
/// <param name="FilePath">Absolute path of the source file.</param>
/// <param name="LineNumber">1-based line in the file.</param>
/// <param name="Section">Section comment preceding the record, if any.</param>
public sealed record SourceRef(string FilePath, int LineNumber, string? Section = null);
