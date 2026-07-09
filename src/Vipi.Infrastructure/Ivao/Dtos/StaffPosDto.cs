using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

internal sealed record StaffPosDto(
    [property: JsonPropertyName("id")] string? Id);
