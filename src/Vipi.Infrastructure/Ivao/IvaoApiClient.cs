using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter HTTP verso le API IVAO v2 (typed HttpClient). Legge il riepilogo ATC online e l'elenco
/// membri divisione, normalizzando in modelli dell'Application. PIANO §7.1. Filtro per prefisso ICAO.
/// </summary>
public sealed class IvaoApiClient : IDivisionMembersProvider
{
    private readonly HttpClient _http;
    private readonly IvaoTokenProvider _token;
    private readonly IvaoOptions _opt;
    private readonly DivisionOptions _div;

    public IvaoApiClient(HttpClient http, IvaoTokenProvider token, IOptions<IvaoOptions> opt, IOptions<DivisionOptions> div)
    {
        _http = http;
        _token = token;
        _opt = opt.Value;
        _div = div.Value;
    }

    /// <summary>ATC della divisione (prefissi ICAO configurati) attualmente online, normalizzati.</summary>
    public async Task<IReadOnlyList<OnlineAtc>> GetOnlineAtcAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Combine(_opt.AtcSummaryPath));
        await AuthorizeAsync(req, ct);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var raw = await res.Content.ReadFromJsonAsync<List<AtcSummaryDto>>(cancellationToken: ct)
                  ?? new List<AtcSummaryDto>();

        return raw
            .Where(a => !string.IsNullOrWhiteSpace(a.Callsign) && MatchesDivision(a.Callsign!))
            .Select(a => new OnlineAtc(
                Callsign: a.Callsign!,
                Vid: a.UserId,
                Name: $"VID {a.UserId}",
                Rating: a.Rating))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DivisionMember>> GetDivisionControllersAsync(CancellationToken ct = default)
    {
        var path = string.Format(_opt.DivisionMembersPathFormat, _div.Code);
        using var req = new HttpRequestMessage(HttpMethod.Get, Combine(path));
        await AuthorizeAsync(req, ct);

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var raw = await res.Content.ReadFromJsonAsync<DivisionMembersDto>(cancellationToken: ct);
        return (raw?.Items ?? new List<DivisionMemberDto>())
            .Select(m => new DivisionMember(m.UserId, m.Name ?? $"VID {m.UserId}", m.AtcRating ?? "—"))
            .ToList();
    }

    private bool MatchesDivision(string callsign) =>
        _div.IcaoPrefixes.Any(p => callsign.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private string Combine(string path) => $"{_opt.BaseUrl.TrimEnd('/')}{path}";

    private async Task AuthorizeAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var token = await _token.GetTokenAsync(ct);
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // DTO permissivi: i campi non noti vengono ignorati.
    private sealed record AtcSummaryDto(
        [property: JsonPropertyName("callsign")] string? Callsign,
        [property: JsonPropertyName("userId")] int UserId,
        [property: JsonPropertyName("rating")] int Rating);

    private sealed record DivisionMembersDto(
        [property: JsonPropertyName("items")] List<DivisionMemberDto>? Items);

    private sealed record DivisionMemberDto(
        [property: JsonPropertyName("userId")] int UserId,
        [property: JsonPropertyName("firstName")] string? Name,
        [property: JsonPropertyName("atcRating")] string? AtcRating);
}
