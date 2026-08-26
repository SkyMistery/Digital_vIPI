using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao.Dtos;
using static Vipi.Infrastructure.Ivao.IvaoHttp;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO v2 per i dettagli per-aeroporto: postazioni ATC, dettaglio postazione (shape/limiti) e piste.
/// Best-effort (in errore la sezione resta da completare a mano). Implementa <see cref="IAirportDetailProvider"/>.
/// Doc refactor 01 §4.2.
/// </summary>
public sealed class IvaoAirportDetailClient : IAirportDetailProvider
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;

    public IvaoAirportDetailClient(IvaoHttp http, IOptions<IvaoOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        var raw = await _http.GetJsonAsync<List<AtcPositionDto>>($"/v2/airports/{Uri.EscapeDataString(icao)}/ATCPositions", ct)
                  ?? new List<AtcPositionDto>();
        return raw
            .Select(p => new SourceAtcPosition(
                Callsign: (p.ComposePosition ?? "").Trim().ToUpperInvariant(),   // es. "LIRN_GND" (NON atcCallsign, che è il nome)
                Frequency: FormatFrequency(p.Frequency),
                Position: p.Position,
                MiddleIdentifier: p.MiddleIdentifier,
                AtcCallsign: p.AtcCallsign,
                IvaoId: p.Id))
            .Where(p => p.Callsign.Length > 0)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default)
    {
        var compose = (composePosition ?? "").Trim().ToUpperInvariant();
        if (compose.Length == 0) return null;

        // Dettaglio per posizione: frequency + regionMapPolygon (+ position/middleIdentifier/limiti se esposti).
        var body = await _http.GetStringAsync(string.Format(_opt.AtcPositionDetailPathFormat, Uri.EscapeDataString(compose)), ct);
        if (body is null) return null;

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var d = doc.RootElement;
        string? polygon = null;
        if (d.TryGetProperty("regionMapPolygon", out var poly) && poly.ValueKind != System.Text.Json.JsonValueKind.Null
            && poly.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            polygon = poly.GetRawText();

        // Coordinate del riferimento aeroporto dal blocco "airport" (presente su ogni postazione dell'aeroporto).
        double? airLat = null, airLon = null;
        if (d.TryGetProperty("airport", out var ap) && ap.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            airLat = JsonNum(ap, "latitude");
            airLon = JsonNum(ap, "longitude");
        }

        return new SourceAtcPosition(
            Callsign: compose,
            Frequency: FormatFrequency(JsonNum(d, "frequency")),
            Position: JsonStr(d, "position"),
            MiddleIdentifier: JsonStr(d, "middleIdentifier"),
            RegionMapPolygon: polygon,
            LowerLimit: JsonNum(d, "lowerLimit") is double lo ? (int)Math.Round(lo) : null,
            UpperLimit: JsonNum(d, "upperLimit") is double up ? (int)Math.Round(up) : null,
            AirportLatitude: airLat,
            AirportLongitude: airLon,
            IvaoId: JsonIntId(d, "id"));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        var raw = await _http.GetJsonAsync<List<RunwayDto>>($"/v2/airports/{Uri.EscapeDataString(icao)}/runways", ct)
                  ?? new List<RunwayDto>();
        return raw
            .Select(r => new SourceRunway(
                Ident: StripRunwayPrefix(r.Runway),
                LengthM: r.Length is double l and > 0 ? (int)Math.Round(l * 0.3048) : null,   // length in piedi → metri
                Bearing: r.Bearing is double b ? (int)Math.Round(b) : null))
            .Where(r => r.Ident.Length > 0)
            .ToList();
    }

    // "RW06" → "06"; "RW24L" → "24L". Toglie il prefisso RW dell'API runways.
    private static string StripRunwayPrefix(string? runway)
    {
        var r = (runway ?? "").Trim().ToUpperInvariant();
        return r.StartsWith("RW") ? r[2..] : r;
    }
}
