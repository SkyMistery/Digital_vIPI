using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

internal sealed record DivisionMemberDto(
    [property: JsonPropertyName("userId")] int UserId,
    [property: JsonPropertyName("firstName")] string? Name,
    [property: JsonPropertyName("atcRating")] string? AtcRating);
