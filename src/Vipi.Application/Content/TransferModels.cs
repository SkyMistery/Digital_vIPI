using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Trasferimento come riga editabile/visualizzabile (catena handler già deserializzata).</summary>
public sealed class TransferRow
{
    public required int Id { get; init; }
    public required string RelationKey { get; init; }
    public required string RelationLabel { get; init; }
    public required TransferPhase Phase { get; init; }
    public required string AirportIcao { get; init; }
    public required string Cop { get; init; }
    public required string FlRule { get; init; }
    public required IReadOnlyList<string> HandlerChain { get; init; }
    public required string StandardFallback { get; init; }
    public required int Order { get; init; }
}

/// <summary>
/// Trasferimento con il "primo online" risolto (F3). <see cref="ResolvedHandler"/> è il primo handler
/// della catena attualmente online; se nessuno è online vale lo <see cref="TransferRow.StandardFallback"/>
/// e <see cref="IsOnline"/> è false.
/// </summary>
public sealed class ResolvedTransferRow
{
    public required TransferRow Row { get; init; }
    public required string ResolvedHandler { get; init; }
    public required bool IsOnline { get; init; }
}

/// <summary>Dati per creare/aggiornare un trasferimento.</summary>
public sealed class TransferInput
{
    public required string RelationKey { get; init; }
    public required string RelationLabel { get; init; }
    public required TransferPhase Phase { get; init; }
    public required string AirportIcao { get; init; }
    public required string Cop { get; init; }
    public required string FlRule { get; init; }
    public required IReadOnlyList<string> HandlerChain { get; init; }
    public required string StandardFallback { get; init; }
}
