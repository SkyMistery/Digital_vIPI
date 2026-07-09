namespace Vipi.Application.Content;

/// <summary>
/// Accumulatore per coppia (Home, Foreign) durante il calcolo adiacenza: min distanza, conteggio coppie
/// settore×settore e insiemi dei settori domestici/esteri adiacenti. Usato da <see cref="NeighbourAdjacencyComputer"/>.
/// </summary>
internal sealed class NeighbourPairAggregate
{
    public string ForeignName = "";
    public string CountryId = "";
    public int Count;
    public double MinDist = double.MaxValue;
    public string? BestForeignPolygon;
    public readonly HashSet<string> HomeSectors = new(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> ForeignSectors = new(StringComparer.OrdinalIgnoreCase);
}
