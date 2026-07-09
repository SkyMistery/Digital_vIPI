using System.Net;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub del sectorfile Aurora IT: scarica itfix/itvor (cache per processo) + <c>&lt;icao&gt;.sid</c>,
/// carica gli alias fix, e delega a <see cref="AuroraSectorfileParser"/>. Repo pubblico raw, nessuna auth.
/// </summary>
public sealed class AuroraSidProvider : ISidProvider
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly ISidFixAliasRepository _aliases;

    // Cache dei navaid (fix+vor): stabili tra i file .sid dello stesso ciclo di import.
    private IReadOnlyDictionary<string, (double, double)>? _navCache;
    private readonly SemaphoreSlim _navLock = new(1, 1);

    public AuroraSidProvider(HttpClient http, IOptions<SectorfileOptions> opt, ISidFixAliasRepository aliases)
    {
        _http = http;
        _opt = opt.Value;
        _aliases = aliases;
    }

    public async Task<IReadOnlyList<SourceSid>> GetSidsAsync(string icao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return Array.Empty<SourceSid>();

        var sidText = await GetTextOrNullAsync($"{icao.Trim().ToLowerInvariant()}.sid", ct);
        if (sidText is null) return Array.Empty<SourceSid>();   // aeroporto senza file SID: nessun import

        var nav = await GetNavaidsAsync(ct);
        var aliasMap = await _aliases.GetMapAsync(ct);
        return AuroraSectorfileParser.ParseSids(icao, sidText, nav, aliasMap);
    }

    private async Task<IReadOnlyDictionary<string, (double, double)>> GetNavaidsAsync(CancellationToken ct)
    {
        if (_navCache is not null) return _navCache;
        await _navLock.WaitAsync(ct);
        try
        {
            if (_navCache is not null) return _navCache;
            var fix = await GetTextOrNullAsync(_opt.FixPath, ct);
            var vor = await GetTextOrNullAsync(_opt.VorPath, ct);
            _navCache = AuroraSectorfileParser.ParseNavaids(fix, vor);
            return _navCache;
        }
        finally { _navLock.Release(); }
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
