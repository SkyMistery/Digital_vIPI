namespace Vipi.Application.Content;

/// <summary>
/// Riga settore ACC (subcenter) per la pagina: chiave naturale + dati sorgente + limiti admin.
/// <paramref name="IsHidden"/> = flag proprio; <paramref name="AccHidden"/> = ACC di appartenenza nascosto
/// (un settore è effettivamente nascosto se IsHidden o AccHidden).
/// </summary>
public sealed record AccSectorRow(
    int Id, string ComposePosition, string CenterId, string? Position, string? MiddleIdentifier,
    string? Frequency, int? LowerLimit, int? UpperLimit, bool IsHidden, bool HasPolygon, bool AccHidden);

/// <summary>
/// Contesto gerarchico d'un settore ATC per validare l'occultamento (Regola 1): un settore RADICE
/// (senza <paramref name="ParentCallsign"/>) non si può nascondere finché ha figli visibili.
/// <paramref name="HasVisibleChildren"/> = esiste ≥1 catalogo (AccSector o AirportSector) non nascosto,
/// con ACC non nascosto, il cui ParentCallsign è questo <paramref name="ComposePosition"/>.
/// </summary>
public sealed record SubcenterHideContext(
    string ComposePosition, string? ParentCallsign, string CenterId, bool HasVisibleChildren)
{
    public bool IsRoot => string.IsNullOrWhiteSpace(ParentCallsign);
}
