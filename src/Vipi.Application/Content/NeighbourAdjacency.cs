namespace Vipi.Application.Content;

/// <summary>Coppia di settori adiacenti (domestico ↔ estero) con la distanza minima tra i bordi.</summary>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record NeighbourAdjacency(string HomeSector, string ForeignSector, double DistanceNm);
