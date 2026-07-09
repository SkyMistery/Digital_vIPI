namespace Vipi.Application.Content;

/// <summary>
/// Riga settore ACC (subcenter) per la pagina: chiave naturale + dati sorgente + limiti admin.
/// <paramref name="IsHidden"/> = flag proprio; <paramref name="AccHidden"/> = ACC di appartenenza nascosto
/// (un settore è effettivamente nascosto se IsHidden o AccHidden).
/// </summary>
public sealed record AccSectorRow(
    int Id, string ComposePosition, string CenterId, string? Position, string? MiddleIdentifier,
    string? Frequency, int? LowerLimit, int? UpperLimit, bool IsHidden, bool HasPolygon, bool AccHidden);
