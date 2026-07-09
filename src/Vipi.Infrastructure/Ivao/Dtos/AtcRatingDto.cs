using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

internal sealed record AtcRatingDto(
    [property: JsonPropertyName("shortName")] string? ShortName);
