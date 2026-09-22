using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Non-generic marker for any file parser, so heterogeneous parsers can be stored in a single
/// map (A's ParserRegistry did, and was dead code: it did not come into vIPI, F2 §2.2). Consumers cast to <see cref="IFileParser{T}"/> when the
/// record type is known. (IFileParser&lt;T&gt; cannot be made covariant because Parse returns the
/// invariant <c>ParseResult&lt;T&gt;</c>, so a non-generic base is used instead of IFileParser&lt;object&gt;.)
/// </summary>
public interface IFileParser;

/// <summary>
/// Parses a single Aurora sector file into a typed domain model.
/// Implementations must be stateless and thread-safe (one instance reused across files/threads).
/// Concrete parsers receive an <see cref="IWarningCollector"/> via constructor injection; they
/// MUST emit non-fatal anomalies via that collector rather than logging or throwing.
/// </summary>
public interface IFileParser<T> : IFileParser
{
    /// <summary>
    /// Parses the file at <paramref name="filePath"/> using the supplied colour palette.
    /// </summary>
    /// <param name="filePath">Absolute path to the file. Must exist and be readable.</param>
    /// <param name="palette">
    /// Active colour palette. Used for colour-name validation only; colours not found in the
    /// palette are accepted and stored verbatim.
    /// </param>
    /// <returns>
    /// A <see cref="ParseResult{T}"/> whose Records list contains every domain object found in
    /// the file, and whose Chunks list covers every line of the file exactly once. Encoding and
    /// HasByteOrderMark reflect how the file was actually read.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the file cannot be read.</exception>
    ParseResult<T> Parse(string filePath, ColorPalette palette);
}
