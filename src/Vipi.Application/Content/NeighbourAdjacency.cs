namespace Vipi.Application.Content;

/// <summary>Coppia di settori adiacenti (domestico ↔ estero) con la distanza minima tra i bordi.</summary>
public sealed record NeighbourAdjacency(string HomeSector, string ForeignSector, double DistanceNm);
