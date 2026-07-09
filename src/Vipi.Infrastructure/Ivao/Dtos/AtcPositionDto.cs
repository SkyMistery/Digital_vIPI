using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

// /v2/airports/{icao}/ATCPositions — es. { composePosition:"LIRN_GND", position:"GND", frequency:121.9 }.
internal sealed record AtcPositionDto(
    [property: JsonPropertyName("composePosition")] string? ComposePosition,
    [property: JsonPropertyName("position")] string? Position,
    [property: JsonPropertyName("middleIdentifier")] string? MiddleIdentifier,
    [property: JsonPropertyName("atcCallsign")] string? AtcCallsign,
    [property: JsonPropertyName("frequency")] double? Frequency);
