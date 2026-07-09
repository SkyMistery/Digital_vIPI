using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

// /v2/users/{UserId}: solo i campi utili al roster staff. Gli altri vengono ignorati.
internal sealed record UserDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("divisionId")] string? DivisionId,
    [property: JsonPropertyName("isStaff")] bool IsStaff,
    [property: JsonPropertyName("publicNickname")] string? PublicNickname,
    [property: JsonPropertyName("rating")] RatingDto? Rating,
    [property: JsonPropertyName("userStaffPositions")] List<StaffPosDto>? UserStaffPositions);
