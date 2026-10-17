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
/// <para>Gli ATC escono <b>tutti</b>, marcati con <c>IsOutsideDivision</c> (dal 28 agosto 2026: prima
/// uscivano filtrati ai prefissi della divisione, e le postazioni del resto del mondo si buttavano). Il
/// filtro è sceso di un piano — lo fa il poller, che sa a cosa serve ogni lista — perché la fotografia è
/// <b>una sola chiamata</b>: le altre postazioni arrivano già pagate, e buttarle era l'unico modo di non
/// poterle più recuperare (il whazzup non ricorda il passato).</para>
///
/// <para>I piloti non si filtrano affatto, di proposito: un volo dentro un settore italiano può avere
/// qualunque callsign, e il filtro giusto è geometrico, non testuale.</para>
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
        // Pubblico: senza token (U-025, vedi IvaoHttp.SendGetPubblicoAsync).
        using var res = await _http.SendGetPubblicoAsync(_opt.WhazzupPath, ct);
        res.EnsureSuccessStatusCode();

        var raw = await res.Content.ReadFromJsonAsync<WhazzupDto>(cancellationToken: ct);

        // 🔴 U-131 (revisione totale 3): una risposta senza gli elenchi non è «nessuno online», è un poll fallito.
        // Prima valeva zero ATC: la cache si svuotava e il poller chiudeva in massa le sessioni aperte, che
        // ricomparendo dopo 15 minuti facevano cadere la scrittura di tutte (U-026). Elenchi PRESENTI e vuoti,
        // invece, sono una risposta: la rete può essere vuota davvero.
        if (raw?.Clients is not { Atcs: not null, Pilots: not null } clients)
            throw new InvalidDataException(
                $"{_opt.WhazzupPath}: risposta senza clients.atcs/clients.pilots — poll fallito, non «nessuno online».");

        var atc = clients.Atcs
            .Where(a => !string.IsNullOrWhiteSpace(a.Callsign))
            .Select(a => (a, Centro: Centro(a.LastTrack)))
            .Select(x => new SourceAtcConnection(
                SessionId: x.a.Id,
                UserId: x.a.UserId,
                Callsign: x.a.Callsign!,
                Position: x.a.AtcSession?.Position,
                Frequency: x.a.AtcSession?.Frequency?.ToString("0.000", CultureInfo.InvariantCulture),
                Rating: x.a.Rating,
                StartUtc: x.a.CreatedAt,
                ConnectedSeconds: x.a.Time,
                AtisLines: x.a.Atis?.Lines,
                IsOutsideDivision: !MatchesDivision(x.a.Callsign!),
                Latitude: x.Centro?.Lat,
                Longitude: x.Centro?.Lon))
            .ToList();

        var pilots = clients.Pilots
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

        // 🔴 U-131: la fotografia porta la data in cui la sorgente l'ha GENERATA. Datata all'arrivo, un whazzup fermo
        // servito con 200 sembrava fresco: la scadenza della cache (T-034) non scattava e il poller la registrava a
        // ogni giro. Senza `updatedAt` (forma vecchia) resta l'ora d'arrivo, come prima.
        return new NetworkSnapshot { Atc = atc, Pilots = pilots, AsOf = raw.UpdatedAt ?? DateTimeOffset.UtcNow };
    }

    /// <summary>
    /// Il centro di una postazione, o <c>null</c> se la sorgente non ne dà uno che si possa usare.
    /// <para>⚠️ <b>Una coordinata sbagliata è peggio di una che manca</b>: chi la riceve la usa per SCARTARE le
    /// postazioni lontane, e una postazione messa per errore dall'altra parte del mondo sparirebbe dal suo conto
    /// senza un avviso. Per questo fuori scala, non-numeri e lo 0,0 esatto (il valore di chi non ha impostato
    /// niente: in mezzo al Golfo di Guinea non c'è nessuna postazione) diventano «non si sa».</para>
    /// </summary>
    internal static (double Lat, double Lon)? Centro(WhazzupAtcTrackDto? track)
    {
        if (track is not { Latitude: { } lat, Longitude: { } lon }) return null;
        if (!double.IsFinite(lat) || !double.IsFinite(lon)) return null;
        if (lat is < -90 or > 90 || lon is < -180 or > 180) return null;
        if (lat == 0 && lon == 0) return null;
        return (lat, lon);
    }

    private bool MatchesDivision(string callsign) =>
        _div.IcaoPrefixes.Any(p => callsign.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
