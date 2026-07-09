using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

// /v2/airports?countryId=…: pagina + solo i campi utili all'editor. centerId = ACC di competenza.
internal sealed record AirportsPageDto(
    [property: JsonPropertyName("items")] List<AirportDto>? Items,
    [property: JsonPropertyName("pages")] int Pages);
