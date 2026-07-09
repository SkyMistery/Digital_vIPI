using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

internal sealed record RatingDto(
    [property: JsonPropertyName("atcRating")] AtcRatingDto? AtcRating);
