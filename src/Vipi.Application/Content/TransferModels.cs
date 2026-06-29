using Vipi.Domain;

namespace Vipi.Application.Content;

// =====================================================================================
//  Coordinamenti/trasferimenti: flussi (settore proprio → tipo/aeroporto) con i loro
//  punti (CoP / livello strutturato / ricevente). DTO di lettura, input ed esito live.
// =====================================================================================

/// <summary>Flusso di traffico di un settore proprio con i suoi punti (lettura).</summary>
public sealed class TransferFlowRow
{
    public required int Id { get; init; }
    public required string AccCode { get; init; }
    public required int OwningSectorId { get; init; }
    public required string OwningSectorCallsign { get; init; }
    public required TransferFlowKind Kind { get; init; }
    public string? AirportIcao { get; init; }
    public string? Description { get; init; }
    public required int Order { get; init; }
    public required IReadOnlyList<TransferPointRow> Points { get; init; }
}

/// <summary>Riga CoP di un flusso (lettura). <see cref="LevelText"/> è il livello già formattato.</summary>
public sealed class TransferPointRow
{
    public required int Id { get; init; }
    public required string Cop { get; init; }
    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public required string LevelText { get; init; }
    public int? NextSectorId { get; init; }
    public string? NextSectorCallsign { get; init; }
    public required int Order { get; init; }
}

/// <summary>Dati per creare/aggiornare un flusso.</summary>
public sealed class TransferFlowInput
{
    public required int OwningSectorId { get; init; }
    public required TransferFlowKind Kind { get; init; }
    public string? AirportIcao { get; init; }
    public string? Description { get; init; }
}

/// <summary>Dati per creare/aggiornare un punto di un flusso.</summary>
public sealed class TransferPointInput
{
    public required string Cop { get; init; }
    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public int? NextSectorId { get; init; }
}

/// <summary>Esito della risoluzione live di un punto (vista operativa): chi prende davvero il traffico ora.</summary>
public sealed class ResolvedTransferPoint
{
    public required TransferPointRow Point { get; init; }
    /// <summary>Callsign del ricevente risolto (primo settore online risalendo la gerarchia), oppure «UNICOM».</summary>
    public required string ResolvedHandler { get; init; }
    public required bool IsOnline { get; init; }
}

/// <summary>Flusso risolto live (vista operativa/Ridotta): il mittente è risolto risalendo la gerarchia
/// (se il settore proprio è chiuso, lo «assorbe» il primo antenato online), e ogni punto ha il ricevente risolto.</summary>
public sealed class ResolvedTransferFlow
{
    public required TransferFlowRow Flow { get; init; }
    /// <summary>Callsign del mittente effettivo: il settore proprio se online, altrimenti il primo antenato online.</summary>
    public required string ResolvedOwnerCallsign { get; init; }
    public required bool OwnerOnline { get; init; }
    public required IReadOnlyList<ResolvedTransferPoint> Points { get; init; }
}
