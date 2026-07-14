using System.Net;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AuroraSidProvider> _log;

    // Cache dei navaid (fix+vor): stabili tra i file .sid dello stesso ciclo di import.
    private IReadOnlyDictionary<string, (double, double)>? _navCache;
    private readonly SemaphoreSlim _navLock = new(1, 1);

    public AuroraSidProvider(HttpClient http, IOptions<SectorfileOptions> opt, ISidFixAliasRepository aliases,
        ILogger<AuroraSidProvider> log)
    {
        _http = http;
        _opt = opt.Value;
        _aliases = aliases;
        _log = log;
    }

    public async Task<IReadOnlyList<SourceSid>> GetSidsAsync(string icao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return Array.Empty<SourceSid>();

        var sidText = await GetTextOrNullAsync($"{icao.Trim().ToLowerInvariant()}.sid", ct);
        if (sidText is null) return Array.Empty<SourceSid>();   // aeroporto senza file SID: nessun import

        var nav = await GetNavaidsAsync(ct);
        var aliasMap = await _aliases.GetMapAsync(ct);
        var sids = AuroraSectorfileParser.ParseSids(icao, sidText, nav, aliasMap);

        // Traccia l'esito: un file .sid presente ma con 0 SID estratti segnala un formato cambiato/corrotto (le righe
        // malformate vengono scartate in silenzio dal parser puro). Senza questo log la degradazione è invisibile.
        var review = sids.Count(s => s.NeedsFixReview);
        if (sids.Count == 0)
            _log.LogWarning("SID {Icao}: file presente ma 0 SID estratti (formato .sid cambiato o corrotto?).", icao);
        else
            _log.LogInformation("SID {Icao}: {Count} estratti ({Review} da verificare fix).", icao, sids.Count, review);
        return sids;
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
