using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao.Dtos;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO della porta <see cref="IAtcActivitySource"/>: una fotografia della rete (chi controlla e chi
/// vola) da <c>/v2/tracker/whazzup</c>.
///
/// <para><b>L'endpoint è pubblico</b>: nessun token, nessuno scope. Misurato il 24 agosto 2026: 705 KB di
/// JSON che sul filo diventano <b>119 KB</b> con Brotli, in 0,21 s, con 467 piloti e 71 ATC. A un giro al
/// minuto fa ~170 MB al giorno — e sostituisce la chiamata a <c>now/atc/summary</c> che il poller faceva
/// già, quindi il numero di chiamate <b>non cambia</b>: si ottengono i piloti in più a costo zero.</para>
///
/// <para>⚠️ La decompressione va abilitata sull'<c>HttpClient</c> (vedi la registrazione in
/// <c>IvaoServiceCollectionExtensions</c>), o si scaricano 705 KB per niente.</para>
///
/// <para>Gli ATC escono <b>filtrati ai prefissi della divisione</b>; i piloti no, di proposito: un volo dentro
/// un settore italiano può avere qualunque callsign, e il filtro giusto è geometrico, non testuale.</para>
/// </summary>
public sealed class IvaoWhazzupClient : IAtcActivitySource
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;
    private readonly DivisionOptions _div;

    public IvaoWhazzupClient(IvaoHttp http, IOptions<IvaoOptions> opt, IOptions<DivisionOptions> div)
    {
        _http = http;
        _opt = opt.Value;
        _div = div.Value;
    }

    public async Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        using var res = await _http.SendGetAsync(_opt.WhazzupPath, ct);
        res.EnsureSuccessStatusCode();

        var raw = await res.Content.ReadFromJsonAsync<WhazzupDto>(cancellationToken: ct);
        var clients = raw?.Clients;

        var atc = (clients?.Atcs ?? new List<WhazzupAtcDto>())
            .Where(a => !string.IsNullOrWhiteSpace(a.Callsign) && MatchesDivision(a.Callsign!))
            .Select(a => new SourceAtcConnection(
                SessionId: a.Id,
                UserId: a.UserId,
                Callsign: a.Callsign!,
                Position: a.AtcSession?.Position,
                Frequency: a.AtcSession?.Frequency?.ToString("0.000", CultureInfo.InvariantCulture),
                Rating: a.Rating,
                StartUtc: a.CreatedAt,
                ConnectedSeconds: a.Time))
            .ToList();

        var pilots = (clients?.Pilots ?? new List<WhazzupPilotDto>())
            .Where(p => !string.IsNullOrWhiteSpace(p.Callsign) && p.LastTrack is not null)
            .Select(p => new SourcePilotFix(
                SessionId: p.Id,
                UserId: p.UserId,
                Callsign: p.Callsign!,
                Latitude: p.LastTrack!.Latitude,
                Longitude: p.LastTrack.Longitude,
                AltitudeFt: p.LastTrack.Altitude,
                GroundSpeed: p.LastTrack.GroundSpeed,
                OnGround: p.LastTrack.OnGround,
                State: p.LastTrack.State,
                DepartureDistanceNm: p.LastTrack.DepartureDistance,
                FlightPlanId: p.FlightPlan?.Id,
                DepIcao: p.FlightPlan?.DepartureId,
                ArrIcao: p.FlightPlan?.ArrivalId,
                AircraftIcao: p.FlightPlan?.AircraftId))
            .ToList();

        return new NetworkSnapshot { Atc = atc, Pilots = pilots, AsOf = DateTimeOffset.UtcNow };
    }

    private bool MatchesDivision(string callsign) =>
        _div.IcaoPrefixes.Any(p => callsign.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
