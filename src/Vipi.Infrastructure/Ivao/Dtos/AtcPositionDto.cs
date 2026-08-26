using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

// /v2/airports/{icao}/ATCPositions — es. { id:3954, composePosition:"LIRN_GND", position:"GND", frequency:121.9 }.
// `id` è l'IDENTITÀ della postazione (numerico, stabile attraverso una rinomina del callsign): arriva già
// nella lista, quindi non costa una chiamata in più.
internal sealed record AtcPositionDto(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("composePosition")] string? ComposePosition,
    [property: JsonPropertyName("position")] string? Position,
    [property: JsonPropertyName("middleIdentifier")] string? MiddleIdentifier,
    [property: JsonPropertyName("atcCallsign")] string? AtcCallsign,
    [property: JsonPropertyName("frequency")] double? Frequency);
