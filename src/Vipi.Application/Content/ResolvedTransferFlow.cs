namespace Vipi.Application.Content;

/// <summary>Flusso risolto live (vista live): il mittente è risolto lungo la catena di ripiego
/// (se il settore proprio è chiuso, lo raccoglie il primo della sua catena che è online), e ogni punto ha il
/// ricevente risolto.
/// <para>⚠️ Lo stesso <see cref="Flow"/> può uscire <b>più volte</b>, una per mittente effettivo: il mittente si
/// risolve alla quota di ogni punto, e due punti a quote diverse possono averne due. Ogni riga porta i soli
/// punti di quel mittente.</para></summary>
public sealed class ResolvedTransferFlow
{
    public required TransferFlowRow Flow { get; init; }
    /// <summary>Callsign del mittente effettivo: il settore proprio se online, altrimenti il primo della sua
    /// catena di ripiego — righe dichiarate che valgono alla quota dei punti, poi i padri — che è online.</summary>
    public required string ResolvedOwnerCallsign { get; init; }
    public required bool OwnerOnline { get; init; }
    public required IReadOnlyList<ResolvedTransferPoint> Points { get; init; }
}
