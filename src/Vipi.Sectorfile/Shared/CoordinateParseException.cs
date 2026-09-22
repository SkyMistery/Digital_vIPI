namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Thrown when a coordinate token cannot be parsed in any supported format,
/// or a fix reference cannot be resolved. Derives from <see cref="FormatException"/>;
/// file parsers catch this and log a warning rather than propagating.
/// </summary>
public sealed class CoordinateParseException : FormatException
{
    public CoordinateParseException(string message) : base(message) { }

    public CoordinateParseException(string message, Exception innerException)
        : base(message, innerException) { }
}
