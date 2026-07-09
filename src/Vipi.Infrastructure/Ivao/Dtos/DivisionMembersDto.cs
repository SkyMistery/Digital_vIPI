using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

internal sealed record DivisionMembersDto(
    [property: JsonPropertyName("items")] List<DivisionMemberDto>? Items);
