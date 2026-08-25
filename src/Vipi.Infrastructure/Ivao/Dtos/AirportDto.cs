using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

internal sealed record AirportDto(
    [property: JsonPropertyName("icao")] string? Icao,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("centerId")] string? CenterId,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("transitionAltitude")] int? TransitionAltitude,
    [property: JsonPropertyName("latitude")] double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude,
    // ⚠️ `military` NON vuol dire «aeroporto militare»: la sorgente lo mette a true anche su Linate, Pisa,
    // Ciampino, Catania, Elmas, Lamezia e Rimini — scali civili con sedime militare. Vuol dire «c'è presenza
    // militare». Misurato il 25 agosto 2026: 34 su 221 aeroporti italiani. La distinzione «solo militare» non
    // sta nella sorgente ed è una scelta editoriale (Airport.IsMilitaryOnly).
    [property: JsonPropertyName("military")] bool? Military,
    [property: JsonPropertyName("iata")] string? Iata,
    // Quota di riferimento in piedi. 43 su 221 nulla (la sorgente non la conosce ovunque).
    [property: JsonPropertyName("elevation")] int? Elevation,
    // Variazione magnetica in gradi. La sorgente la manda ora intera ora decimale (misurato: int e float
    // nella stessa pagina), quindi double e non int, o i decimali si perdono in silenzio.
    [property: JsonPropertyName("magnetic")] double? Magnetic);
