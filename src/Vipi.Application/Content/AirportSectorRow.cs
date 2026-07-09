namespace Vipi.Application.Content;

/// <summary>
/// Riga settore ATC d'aeroporto per l'editor: chiave naturale + dati sorgente + limiti admin.
/// <paramref name="IsHidden"/> = flag proprio (l'aeroporto non si nasconde, quindi niente flag derivato).
/// </summary>
public sealed record AirportSectorRow(
    int Id, string ComposePosition, string AirportIcao, string AccCode, string? Position,
    string? MiddleIdentifier, string? Frequency, int? LowerLimit, int? UpperLimit, bool IsHidden, bool HasPolygon, bool IsPrimary,
    bool IsAccApp, bool LimitsFromSource = false);
