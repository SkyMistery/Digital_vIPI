using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub del file poligoni TWR di Aurora IT (<c>DYNAMIC_SEC/twrs.tfl</c>): scarica il file (repo pubblico
/// raw, no auth), delega il parsing DMS a <see cref="AuroraSectorfileParser.ParseTowerShapes"/> e converte ogni
/// anello nel JSON <c>RegionMapPolygon</c> (coppie <c>[lng, lat]</c>, stile GeoJSON). Il risultato è messo in cache
/// di processo da <see cref="SectorfileCache"/>. Lifetime transient (registrato con <c>AddHttpClient&lt;,&gt;</c>):
/// nessuno stato condiviso qui dentro.
/// </summary>
public sealed class AuroraTowerShapeProvider : ITowerShapeSource
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly SectorfileCache _cache;
    private readonly ILogger<AuroraTowerShapeProvider> _log;

    public AuroraTowerShapeProvider(HttpClient http, IOptions<SectorfileOptions> opt, SectorfileCache cache,
        ILogger<AuroraTowerShapeProvider> log)
    {
        _http = http;
        _opt = opt.Value;
        _cache = cache;
        _log = log;
    }

    public Task<IReadOnlyDictionary<string, string>> GetTowerPolygonsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return Task.FromResult(EmptyMap);

        return _cache.GetTowerPolygonsAsync(async token =>
        {
            var text = await SectorfileRaw.GetTextOrNullAsync(_http, _opt.RawBaseUrl, _opt.TwrShapePath, token);
            if (text is null)
            {
                _log.LogWarning("Shape TWR: {Path} non trovato (404).", _opt.TwrShapePath);
                return EmptyMap;
            }

            var rings = AuroraSectorfileParser.ParseTowerShapes(text);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (callsign, ring) in rings)
                map[callsign] = ToPolygonJson(ring);

            _log.LogInformation("Shape TWR da GitHub: {Count} poligoni parsati da {Path}.", map.Count, _opt.TwrShapePath);
            return map;
        }, ct);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Anello (Lat, Lon) → JSON <c>[[lng,lat],…]</c> (longitudine prima, coerente con RegionMapPolygon IVAO).</summary>
    private static string ToPolygonJson(IReadOnlyList<(double Lat, double Lon)> ring)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < ring.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[')
              .Append(Math.Round(ring[i].Lon, 6).ToString(CultureInfo.InvariantCulture))
              .Append(',')
              .Append(Math.Round(ring[i].Lat, 6).ToString(CultureInfo.InvariantCulture))
              .Append(']');
        }
        return sb.Append(']').ToString();
    }
}
