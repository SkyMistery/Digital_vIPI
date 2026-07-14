using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Flusso di traffico di un settore proprio con i suoi punti (lettura).</summary>
public sealed class TransferFlowRow
{
    public required int Id { get; init; }
    public required string AccCode { get; init; }
    public required int OwningSectorId { get; init; }
    public required string OwningSectorCallsign { get; init; }
    public required TransferFlowKind Kind { get; init; }
    public string? AirportIcao { get; init; }
    /// <summary>Nome per aeroporti fuori DB (nuovi/esteri); null se in DB (nome dal catalogo).</summary>
    public string? AirportName { get; init; }
    public string? Description { get; init; }
    public required int Order { get; init; }
    public required IReadOnlyList<TransferPointRow> Points { get; init; }
}
