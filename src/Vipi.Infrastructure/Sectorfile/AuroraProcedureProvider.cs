using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub del sectorfile Aurora IT: scarica <c>&lt;icao&gt;.sid</c> (partenze) o <c>&lt;icao&gt;.str</c>
/// (arrivi), prende il catalogo dei punti da <see cref="INavaidSource"/>, carica gli alias fix, e delega a
/// <see cref="AuroraSectorfileParser"/>. Repo pubblico raw, nessuna auth. Lifetime transient (registrato con
/// <c>AddHttpClient&lt;,&gt;</c>): nessuno stato condiviso qui.
/// </summary>
internal sealed class AuroraProcedureProvider : IProcedureProvider
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly ISidFixAliasRepository _aliases;
    private readonly INavaidSource _navaids;
    private readonly ILogger<AuroraProcedureProvider> _log;

    public AuroraProcedureProvider(HttpClient http, IOptions<SectorfileOptions> opt, ISidFixAliasRepository aliases,
        INavaidSource navaids, ILogger<AuroraProcedureProvider> log)
    {
        _http = http;
        _opt = opt.Value;
        _aliases = aliases;
        _navaids = navaids;
        _log = log;
    }

    public async Task<IReadOnlyList<SourceProcedure>> GetAsync(string icao, ProcedureKind kind, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return Array.Empty<SourceProcedure>();

        // I due file si chiamano come il verso della procedura, e il resto della lettura è identico.
        var estensione = kind == ProcedureKind.Star ? "str" : "sid";
        var testo = await GetTextOrNullAsync($"{icao.Trim().ToLowerInvariant()}.{estensione}", ct);
        if (testo is null) return Array.Empty<SourceProcedure>();   // aeroporto senza quel file: nessun import

        var nav = await _navaids.GetAsync(ct);
        var aliasMap = await _aliases.GetMapAsync(ct);
        var righe = kind == ProcedureKind.Star
            ? AuroraSectorfileParser.ParseStars(icao, testo, nav.Names, aliasMap)
            : AuroraSectorfileParser.ParseSids(icao, testo, nav.Names, aliasMap);

        // Traccia l'esito: un file presente ma con 0 procedure estratte segnala un formato cambiato/corrotto (le
        // righe malformate vengono scartate in silenzio dal parser puro). Senza questo log la degradazione è
        // invisibile.
        // ⚠️ Per le STAR lo zero è NORMALE su 36 dei 90 `.str`: quei file sono tutti voci del menu mappe. È un
        // avviso da leggere come «guarda se il formato è cambiato», non come un guasto.
        var review = righe.Count(s => s.NeedsFixReview);
        if (righe.Count == 0)
            _log.LogWarning("{Kind} {Icao}: file presente ma 0 procedure estratte (formato .{Ext} cambiato o corrotto?).",
                kind, icao, estensione);
        else
            _log.LogInformation("{Kind} {Icao}: {Count} estratte ({Review} da verificare fix).", kind, icao, righe.Count, review);
        return righe;
    }

    private Task<string?> GetTextOrNullAsync(string relative, CancellationToken ct) =>
        SectorfileRaw.GetTextOrNullAsync(_http, _opt.RawBaseUrl, relative, ct);
}
