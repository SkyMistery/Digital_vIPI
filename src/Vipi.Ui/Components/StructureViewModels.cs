namespace Vipi.Ui.Components;

/// <summary>Riga «figlio coperto» nell'anteprima copertura (dominio giù) di un nodo di struttura.</summary>
public sealed record CoverageChildRow(string Name, string Badge, string BadgeClass, int DescendantCount);

/// <summary>Riga «antenato» nella catena di fallback (dominio su) di un nodo di struttura.</summary>
public sealed record FallbackChainRow(string Callsign, string Badge, string BadgeClass, string AccCode);
