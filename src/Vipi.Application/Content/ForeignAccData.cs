using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Dati di un ACC estero scaricati dalla sorgente (prima del filtro di adiacenza): codice, nome, paese e i
/// suoi subcenter. Prodotto da <see cref="ForeignAccFetcher"/>, consumato da <see cref="NeighbourAdjacencyComputer"/>.
/// </summary>
public sealed record ForeignAccData(string Code, string Name, string Country, IReadOnlyList<SourceSubcenter> Subcenters);
