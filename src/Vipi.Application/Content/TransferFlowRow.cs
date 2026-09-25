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
    /// <summary>
    /// Tutti gli aeroporti dell'accordo, nell'ordine scritto, quando sono PIU' D'UNO; vuoto altrimenti.
    /// <para>🔴 25 settembre 2026, chiesto dal committente: un accordo per LICC e LICZ si espande in un flusso per
    /// aeroporto, e la tabella lo richiude in UNA riga per clausola — la frase che sopravviveva era quella del
    /// primo, «con destinazione Catania Fontanarossa LICC», e Sigonella spariva dal testo. La frase li dice tutti.</para>
    /// </summary>
    public IReadOnlyList<string> AirportIcaos { get; init; } = Array.Empty<string>();
    public string? Description { get; init; }
    public required int Order { get; init; }
    public required IReadOnlyList<TransferPointRow> Points { get; init; }
}
