namespace Vipi.Ui.Components;

/// <summary>Riga «figlio coperto» nell'anteprima copertura (dominio giù) di un nodo di struttura.</summary>
public sealed record CoverageChildRow(string Name, string Badge, string BadgeClass, int DescendantCount);

/// <summary>
/// Una voce della catena di ripiego come si mostra: chi raccoglie, in quale fascia di quota, e se è una
/// riga <b>scritta</b> o il <b>padre</b> di copertura — che nessuno scrive e che non si può togliere.
/// </summary>
/// <param name="Banda">Testo della fascia già composto (es. «FL325–UNL»); <c>null</c> = vale a ogni quota.</param>
/// <param name="DalPadre">Vero se la voce è il padre di copertura, cioè la coda implicita della catena.</param>
/// <param name="Online">Vero se quel settore è in frequenza adesso: è la voce che il traffico prenderebbe.</param>
public sealed record FallbackChainRow(
    string Callsign, string Badge, string BadgeClass, string AccCode,
    string? Banda = null, bool DalPadre = false, bool Online = false);
