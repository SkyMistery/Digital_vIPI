using System.Net.Http.Json;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao.Dtos;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO v2 per il profilo del singolo utente (/v2/users/{id}): nickname, rating ATC, staff.
/// Best-effort (null su 404/transitorio). Implementa la porta <see cref="IUserDirectory"/>. Doc refactor 01 §4.2.
/// </summary>
public sealed class IvaoUserClient : IUserDirectory
{
    private readonly IvaoHttp _http;

    public IvaoUserClient(IvaoHttp http) => _http = http;

    /// <inheritdoc />
    public async Task<SourceUserStaff?> GetUserAsync(int UserId, CancellationToken ct = default)
    {
        if (!_http.IsConfigured) return null;

        using var res = await _http.SendGetAsync($"/v2/users/{UserId}", ct);
        if (!res.IsSuccessStatusCode) return null;   // 404/transitorio: il chiamante non tocca lo stato

        var u = await res.Content.ReadFromJsonAsync<UserDto>(cancellationToken: ct);
        if (u is null) return null;

        var positions = (u.UserStaffPositions ?? new List<StaffPosDto>())
            .Select(p => p.Id)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();

        return new SourceUserStaff(
            UserId: u.Id,
            Nickname: NormalizeNick(u.PublicNickname, u.Id),
            AtcRating: u.Rating?.AtcRating?.ShortName,
            DivisionId: u.DivisionId,
            IsStaff: u.IsStaff,
            StaffPositionCodes: positions);
    }

    // L'API restituisce un nickname placeholder "User {UserId}" quando l'utente non ne ha impostato uno.
    private static string? NormalizeNick(string? nick, int UserId) =>
        string.IsNullOrWhiteSpace(nick) || nick.Trim().Equals($"User {UserId}", StringComparison.OrdinalIgnoreCase)
            ? null : nick.Trim();
}
