using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

// DTO permissivi: i campi non noti vengono ignorati.
internal sealed record AtcSummaryDto(
    [property: JsonPropertyName("callsign")] string? Callsign,
    [property: JsonPropertyName("userId")] int UserId,
    [property: JsonPropertyName("rating")] int Rating);
