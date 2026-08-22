using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Adapter GitHub delle carte MRVA di Aurora IT: scarica il file <c>.mva</c> dell'ente (repo pubblico raw, nessuna
/// auth), delega il parsing a <see cref="AuroraSectorfileParser.ParseMva"/> e mette il risultato in cache di
/// processo (<see cref="SectorfileCache"/>). Lifetime transient (registrato con <c>AddHttpClient&lt;,&gt;</c>):
/// nessuno stato condiviso qui dentro.
/// <para>
/// I percorsi sono due e stanno qui, non in <see cref="SectorfileOptions"/>, come per le SID: non sono un file
/// singolo configurabile ma uno <b>schema di nome</b> imposto dal sectorfile — l'enroute di un ACC vive in
/// <c>ENRMVA/{acc}.mva</c> (caricato dagli <c>.isc</c> con un <c>F;</c> esplicito nella sezione <c>[MVAENR]</c>),
/// l'aeroporto in <c>{icao}.mva</c> nella root (auto-load per ICAO). Nomi in minuscolo: è la convenzione del
/// repository e i raw di GitHub sono case-sensitive.
/// </para>
/// </summary>
public sealed class AuroraMvaProvider : IVectoringMinimaSource
{
    private readonly HttpClient _http;
    private readonly SectorfileOptions _opt;
    private readonly SectorfileCache _cache;
    private readonly ILogger<AuroraMvaProvider> _log;

    public AuroraMvaProvider(HttpClient http, IOptions<SectorfileOptions> opt, SectorfileCache cache,
        ILogger<AuroraMvaProvider> log)
    {
        _http = http;
        _opt = opt.Value;
        _cache = cache;
        _log = log;
    }

    public Task<MvaChart> GetAccChartAsync(string accCode, CancellationToken ct = default) =>
        GetChartAsync(accCode, $"ENRMVA/{Norm(accCode)}.mva", ct);

    public Task<MvaChart> GetAirportChartAsync(string icao, CancellationToken ct = default) =>
        GetChartAsync(icao, $"{Norm(icao)}.mva", ct);

    private Task<MvaChart> GetChartAsync(string? code, string relative, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl) || string.IsNullOrWhiteSpace(code))
            return Task.FromResult(MvaChart.Empty);

        // La chiave di cache è il percorso: distingue da sola ENRMVA/lipp.mva dall'ipotetico lipp.mva di root.
        return _cache.GetMvaChartAsync(relative, async token =>
        {
            var text = await SectorfileRaw.GetTextOrNullAsync(_http, _opt.RawBaseUrl, relative, token);
            if (text is null)
            {
                // 404 = caso normale, non un guasto: 25 APP su 49 non hanno il file, e nel sectorfile è
                // indistinguibile «non serve» da «non l'ha ancora fatto nessuno» (nessuna componente è obbligatoria).
                _log.LogDebug("MRVA: {Path} non presente nel sectorfile.", relative);
                return MvaChart.Empty;
            }

            var chart = AuroraSectorfileParser.ParseMva(text);

            // Un file presente ma illeggibile è l'unico caso che vale un avviso: il parser scarta le righe
            // malformate in silenzio, quindi senza questo log un cambio di formato a monte sparirebbe.
            if (chart.IsEmpty)
                _log.LogWarning("MRVA: {Path} presente ma senza contenuto leggibile (formato .mva cambiato?).", relative);
            else
                _log.LogInformation("MRVA {Path}: {Shapes} tracciati, {Labels} etichette.",
                    relative, chart.Shapes.Count, chart.Labels.Count);
            return chart;
        }, ct);
    }

    private static string Norm(string code) => code.Trim().ToLowerInvariant();
}
