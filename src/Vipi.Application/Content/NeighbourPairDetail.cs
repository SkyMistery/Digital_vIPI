namespace Vipi.Application.Content;

/// <summary>
/// Dettaglio di verifica di una coppia ACC confinante: elenco delle adiacenze settore↔settore (con distanza) e le
/// forme proiettate in un viewBox condiviso (settori domestici + esteri) per il disegno della mappa. Calcolato
/// on-demand (ri-scarica i subcenter esteri da IVAO), non persistito.
/// </summary>
public sealed record NeighbourPairDetail(
    string HomeAccCode, string ForeignAccCode,
    IReadOnlyList<NeighbourAdjacency> Adjacencies,
    string? MapViewBox,
    IReadOnlyList<NeighbourMapShape> HomeShapes,
    IReadOnlyList<NeighbourMapShape> ForeignShapes,
    IReadOnlyList<string> Warnings);
