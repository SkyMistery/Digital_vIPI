namespace Vipi.Application.Content;

/// <summary>Singola adiacenza settore domestico↔estero trovata durante il calcolo import (per il logging di debug).</summary>
public sealed record NeighbourHit(string Home, string HomeSector, string Foreign, string ForeignSector, double DistanceNm);
