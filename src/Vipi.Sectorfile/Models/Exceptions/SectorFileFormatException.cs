namespace Vipi.Sectorfile.Models;

/// <summary>
/// Thrown for non-recoverable sector-file format errors. Recoverable anomalies are emitted
/// as warnings via <c>IWarningCollector</c> rather than thrown.
/// </summary>
public sealed class SectorFileFormatException : Exception
{
    public SectorFileFormatException(string message) : base(message) { }

    public SectorFileFormatException(string message, Exception innerException)
        : base(message, innerException) { }
}
