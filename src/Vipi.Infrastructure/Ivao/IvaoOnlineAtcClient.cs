using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao.Dtos;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO v2 per il riepilogo ATC online, filtrato ai prefissi ICAO della divisione. Fetch grezzo usato
/// dal poller (<see cref="AtcPollingHostedService"/>), che ne salva l'esito in <see cref="OnlineAtcCache"/>
/// (porta <see cref="IOnlineAtcProvider"/>). Doc refactor 01 §4.2.
/// </summary>
public sealed class IvaoOnlineAtcClient
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;
    private readonly DivisionOptions _div;

    public IvaoOnlineAtcClient(IvaoHttp http, IOptions<IvaoOptions> opt, IOptions<DivisionOptions> div)
    {
        _http = http;
        _opt = opt.Value;
        _div = div.Value;
    }

    /// <summary>ATC della divisione (prefissi ICAO configurati) attualmente online, normalizzati.</summary>
    public async Task<IReadOnlyList<OnlineAtc>> GetOnlineAtcAsync(CancellationToken ct = default)
    {
        using var res = await _http.SendGetAsync(_opt.AtcSummaryPath, ct);
        res.EnsureSuccessStatusCode();

        var raw = await res.Content.ReadFromJsonAsync<List<AtcSummaryDto>>(cancellationToken: ct)
                  ?? new List<AtcSummaryDto>();

        return raw
            .Where(a => !string.IsNullOrWhiteSpace(a.Callsign) && MatchesDivision(a.Callsign!))
            .Select(a => new OnlineAtc(
                Callsign: a.Callsign!,
                UserId: a.UserId,
                Name: $"UserId {a.UserId}",
                Rating: a.Rating))
            .ToList();
    }

    private bool MatchesDivision(string callsign) =>
        _div.IcaoPrefixes.Any(p => callsign.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
