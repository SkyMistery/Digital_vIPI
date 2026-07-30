using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub del sectorfile Aurora IT: scarica itfix/itvor (cache di processo in <see cref="SectorfileCache"/>)
/// + <c>&lt;icao&gt;.sid</c>, carica gli alias fix, e delega a <see cref="AuroraSectorfileParser"/>. Repo pubblico raw,
/// nessuna auth. Lifetime transient (registrato con <c>AddHttpClient&lt;,&gt;</c>): nessuno stato condiviso qui dentro.
/// </summary>
public sealed class AuroraSidProvider : ISidProvider
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly ISidFixAliasRepository _aliases;
    private readonly SectorfileCache _cache;
    private readonly ILogger<AuroraSidProvider> _log;

    public AuroraSidProvider(HttpClient http, IOptions<SectorfileOptions> opt, ISidFixAliasRepository aliases,
        SectorfileCache cache, ILogger<AuroraSidProvider> log)
    {
        _http = http;
        _opt = opt.Value;
        _aliases = aliases;
        _cache = cache;
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

    // I nomi navaid sono stabili tra i file .sid dello stesso ciclo: caricati una volta per processo dalla cache
    // condivisa (non da un campo d'istanza, che con lifetime transient sarebbe una cache per-risoluzione).
    private Task<IReadOnlySet<string>> GetNavaidsAsync(CancellationToken ct) =>
        _cache.GetNavaidsAsync(async token =>
        {
            var fix = await GetTextOrNullAsync(_opt.FixPath, token);
            var vor = await GetTextOrNullAsync(_opt.VorPath, token);
            return AuroraSectorfileParser.ParseNavaids(fix, vor);
        }, ct);

    private Task<string?> GetTextOrNullAsync(string relative, CancellationToken ct) =>
        SectorfileRaw.GetTextOrNullAsync(_http, _opt.RawBaseUrl, relative, ct);
}
