using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

// /v2/airports/{icao}/runways — una riga PER SOGLIA. Misurato sul filo il 29 agosto 2026 su LIPI:
// { id:11609, airportIcao:"LIPI", runway:"RW06", length:8383, bearing:57,
//   latitude:45.9735305556, longitude:13.0350638889, elevation:162, width:44 }
//
// ⚠️ `length` è in METRI dal 7 settembre 2026. Nella risposta qui sopra — che è del 29 agosto — gli 8383
// erano PIEDI, e il client li convertiva. Il campo non ha cambiato nome quando ha cambiato unità, quindi
// l'unica cosa che distingue le due epoche è questa riga: leggerla prima di fidarsi dell'esempio.
//
// ⚠️ Latitudine, longitudine ed elevazione arrivavano da sempre e non le leggevamo: di otto campi ne
// mappavamo quattro. Le coordinate della soglia sono quel che il SOP militare stampa in tabella, e il dato
// si perdeva in traduzione.
internal sealed record RunwayDto(
    [property: JsonPropertyName("runway")] string? Runway,
    [property: JsonPropertyName("length")] double? Length,
    [property: JsonPropertyName("width")] double? Width,
    [property: JsonPropertyName("bearing")] double? Bearing,
    [property: JsonPropertyName("latitude")] double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude,
    [property: JsonPropertyName("elevation")] double? Elevation);
