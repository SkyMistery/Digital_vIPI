namespace Vipi.Application.Content;

/// <summary>Flusso risolto live (vista live): il mittente è risolto risalendo la gerarchia
/// (se il settore proprio è chiuso, lo «assorbe» il primo antenato online), e ogni punto ha il ricevente risolto.</summary>
public sealed class ResolvedTransferFlow
{
    public required TransferFlowRow Flow { get; init; }
    /// <summary>Callsign del mittente effettivo: il settore proprio se online, altrimenti il primo antenato online.</summary>
    public required string ResolvedOwnerCallsign { get; init; }
    public required bool OwnerOnline { get; init; }
    public required IReadOnlyList<ResolvedTransferPoint> Points { get; init; }
}
