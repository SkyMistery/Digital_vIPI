using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Dati per creare/aggiornare un flusso.</summary>
public sealed class TransferFlowInput
{
    public required int OwningSectorId { get; init; }
    public required TransferFlowKind Kind { get; init; }
    public string? AirportIcao { get; init; }
    /// <summary>Nome dell'aeroporto quando è fuori DB (nuovo/estero); ignorato se l'ICAO è nel catalogo.</summary>
    public string? AirportName { get; init; }
    public string? Description { get; init; }
}
