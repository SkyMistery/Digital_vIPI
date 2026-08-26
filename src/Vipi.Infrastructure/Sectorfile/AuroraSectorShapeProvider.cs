using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub dei poligoni di <b>settore</b> (CTR/APP/MIL/FSS) del sectorfile Aurora IT. Gemello di
/// <see cref="AuroraTowerShapeProvider"/>, che fa lo stesso per le sole TWR.
///
/// <para><b>Quali file leggere lo dice Aurora.</b> Non c'è un elenco scritto qui e non si chiede a GitHub
/// che cosa contenga la cartella: si legge <c>ITALY.isc</c>, il file maestro che Aurora stessa carica, e si
/// prendono le sue righe <c>F;DYNAMIC_SEC\…tfl</c>. Così un settore aggiunto domani entra da sé, e uno
/// tolto dall'indice smette di entrare — senza che nessuno debba ricordarsi di aggiornare una lista.</para>
///
/// <para>⚠️ <c>twrs.tfl</c> è nell'indice ma <b>si salta</b>: ha già il suo provider, e leggerlo due volte
/// vorrebbe dire due strade che scrivono la stessa shape con regole diverse.</para>
///
/// <para>Lifetime transient (<c>AddHttpClient&lt;,&gt;</c>): nessuno stato qui dentro. Il risultato lo tiene
/// <see cref="SectorfileCache"/>.</para>
/// </summary>
public sealed class AuroraSectorShapeProvider : ISectorShapeSource
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly SectorfileCache _cache;
    private readonly INavaidSource _navaids;
    private readonly ILogger<AuroraSectorShapeProvider> _log;

    public AuroraSectorShapeProvider(HttpClient http, IOptions<SectorfileOptions> opt, SectorfileCache cache,
        INavaidSource navaids, ILogger<AuroraSectorShapeProvider> log)
    {
        _http = http;
        _opt = opt.Value;
        _cache = cache;
        _navaids = navaids;
        _log = log;
    }

    /// <summary>Le righe dell'indice che citano un file di settore: <c>F;DYNAMIC_SEC\lirr_ne_ctr.tfl</c>.
    /// La barra è quella di Windows perché l'indice lo legge Aurora, che gira lì.</summary>
    private static readonly Regex RigaSettore = new(
        @"^\s*F\s*;\s*(DYNAMIC_SEC[\\/][A-Za-z0-9_.-]+\.tfl)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public Task<SectorShapes> GetSectorPolygonsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl) || string.IsNullOrWhiteSpace(_opt.SectorIndexUrl))
            return Task.FromResult(SectorShapes.Empty);

        return _cache.GetSectorPolygonsAsync(async token =>
        {
            var indice = await ScaricaIndiceAsync(token);
            if (indice is null)
            {
                _log.LogWarning("Shape settori: indice {Url} non raggiungibile.", _opt.SectorIndexUrl);
                return SectorShapes.Empty;
            }

            var files = FileDiSettore(indice);
            if (files.Count == 0)
            {
                _log.LogWarning("Shape settori: {Url} non cita nessun file di settore.", _opt.SectorIndexUrl);
                return SectorShapes.Empty;
            }

            // I nomi di punto dei vertici si risolvono qui: senza catalogo i blocchi che li usano si scartano,
            // e il chiamante lo legge da UnresolvedPoints invece di trovarsi settori senza area e basta.
            var punti = await _navaids.GetAsync(token);

            var poligoni = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var irrisolti = new List<(string, string)>();
            foreach (var rel in files)
            {
                var testo = await SectorfileRaw.GetTextOrNullAsync(_http, _opt.RawBaseUrl, rel, token);
                if (testo is null) { _log.LogDebug("Shape settori: {Path} non trovato (404).", rel); continue; }

                var parse = AuroraSectorfileParser.ParseSectorShapes(testo, punti);
                foreach (var (cs, ring) in parse.Rings)
                    poligoni[cs] = AuroraSectorfileParser.RingToPolygonJson(ring);
                irrisolti.AddRange(parse.UnresolvedPoints);
            }

            _log.LogInformation(
                "Shape settori dal sectorfile: {Count} callsign da {Files} file; {Unresolved} blocchi scartati per punti non in catalogo.",
                poligoni.Count, files.Count, irrisolti.Count);

            return new SectorShapes(poligoni, irrisolti);
        }, ct);
    }

    private async Task<string?> ScaricaIndiceAsync(CancellationToken ct)
    {
        // L'indice sta un livello SOPRA RawBaseUrl (che punta a Include/IT/), quindi ha un URL suo intero
        // invece di un path relativo: `..` su raw.githubusercontent non si risolve.
        using var resp = await _http.GetAsync(_opt.SectorIndexUrl, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>I file di settore citati dall'indice, in ordine, senza ripetizioni e senza le TWR.</summary>
    /// <remarks>⚠️ Le ripetizioni ci sono davvero: <c>ITALY.isc</c> cita <c>lirrctr.tfl</c> due volte di
    /// seguito. Leggerlo due volte non romperebbe niente — la seconda riscrive la stessa chiave — ma
    /// raddoppierebbe una richiesta di rete per niente.</remarks>
    public static IReadOnlyList<string> FileDiSettore(string indice)
    {
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        foreach (Match m in RigaSettore.Matches(indice))
        {
            var rel = m.Groups[1].Value.Replace('\\', '/');
            if (rel.EndsWith("/twrs.tfl", StringComparison.OrdinalIgnoreCase)) continue;   // ha il suo provider
            if (visti.Add(rel)) files.Add(rel);
        }
        return files;
    }
}
