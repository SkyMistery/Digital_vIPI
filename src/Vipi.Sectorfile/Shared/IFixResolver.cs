namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Resolves a fix/navaid identifier to a coordinate. Defined in Shared so that
/// <see cref="CoordinateConverter"/> can resolve fix-reference tokens at render time
/// without depending on the Models layer (which owns the concrete <c>NavaidSet</c>).
/// The Models <c>NavaidSet</c> implements this interface; render-time consumers pass it in.
/// </summary>
public interface IFixResolver
{
    /// <summary>Attempts to resolve a fix/navaid identifier to its coordinate.</summary>
    bool TryResolve(string ident, out Coordinate position);
}
