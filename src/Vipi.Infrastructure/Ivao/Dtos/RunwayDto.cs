using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

// /v2/airports/{icao}/runways — es. { runway:"RW06", length:8622 (piedi), width:45, bearing:56 }.
internal sealed record RunwayDto(
    [property: JsonPropertyName("runway")] string? Runway,
    [property: JsonPropertyName("length")] double? Length,
    [property: JsonPropertyName("width")] double? Width,
    [property: JsonPropertyName("bearing")] double? Bearing);
