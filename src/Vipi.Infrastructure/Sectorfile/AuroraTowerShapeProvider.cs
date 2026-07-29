using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub del file poligoni TWR di Aurora IT (<c>DYNAMIC_SEC/twrs.tfl</c>): scarica il file (repo pubblico
/// raw, no auth), delega il parsing DMS a <see cref="AuroraSectorfileParser.ParseTowerShapes"/> e converte ogni
/// anello nel JSON <c>RegionMapPolygon</c> (coppie <c>[lng, lat]</c>, stile GeoJSON). Cache per processo.
/// </summary>
public sealed class AuroraTowerShapeProvider : ITowerShapeSource
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly ILogger<AuroraTowerShapeProvider> _log;

    private IReadOnlyDictionary<string, string>? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AuroraTowerShapeProvider(HttpClient http, IOptions<SectorfileOptions> opt, ILogger<AuroraTowerShapeProvider> log)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetTowerPolygonsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return EmptyMap;
        if (_cache is not null) return _cache;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache is not null) return _cache;

            var text = await GetTextOrNullAsync(_opt.TwrShapePath, ct);
            if (text is null) { _log.LogWarning("Shape TWR: {Path} non trovato (404).", _opt.TwrShapePath); return _cache = EmptyMap; }

            var rings = AuroraSectorfileParser.ParseTowerShapes(text);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (callsign, ring) in rings)
                map[callsign] = ToPolygonJson(ring);

            _log.LogInformation("Shape TWR da GitHub: {Count} poligoni parsati da {Path}.", map.Count, _opt.TwrShapePath);
            return _cache = map;
        }
        finally { _lock.Release(); }
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

    private async Task<string?> GetTextOrNullAsync(string relative, CancellationToken ct)
    {
        var url = _opt.RawBaseUrl.TrimEnd('/') + "/" + relative.TrimStart('/');
        using var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }
}
