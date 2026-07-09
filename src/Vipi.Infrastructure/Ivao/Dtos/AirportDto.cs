using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

internal sealed record AirportDto(
    [property: JsonPropertyName("icao")] string? Icao,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("centerId")] string? CenterId,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("transitionAltitude")] int? TransitionAltitude,
    [property: JsonPropertyName("latitude")] double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude);
