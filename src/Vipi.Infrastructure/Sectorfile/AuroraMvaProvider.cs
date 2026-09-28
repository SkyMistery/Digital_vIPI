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
    private readonly Persistence.EfMvaChartStates? _stati;
    private readonly Vipi.Application.Content.ShapeReleaseContext? _cattura;

    /// <param name="stati">U-037: le carte ricordate col testo in vigore. Senza, il provider legge il sectorfile
    /// com'è — il comportamento di prima.</param>
    /// <param name="cattura">U-037: il ciclo della release che si sta congelando. Dentro la cattura, una carta
    /// cambiata per un ciclo che la release non ha ancora raggiunto esce com'era.</param>
    public AuroraMvaProvider(HttpClient http, IOptions<SectorfileOptions> opt, SectorfileCache cache,
        ILogger<AuroraMvaProvider> log, Persistence.EfMvaChartStates? stati = null,
        Vipi.Application.Content.ShapeReleaseContext? cattura = null)
    {
        _http = http;
        _opt = opt.Value;
        _cache = cache;
        _log = log;
        _stati = stati;
        _cattura = cattura;
    }

    public Task<MvaChart> GetAccChartAsync(string accCode, CancellationToken ct = default) =>
        GetChartAsync(accCode, $"ENRMVA/{Norm(accCode)}.mva", ct);

    public Task<MvaChart> GetAirportChartAsync(string icao, CancellationToken ct = default) =>
        GetChartAsync(icao, $"{Norm(icao)}.mva", ct);

    private async Task<MvaChart> GetChartAsync(string? code, string relative, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl) || string.IsNullOrWhiteSpace(code))
            return MvaChart.Empty;

        // La chiave di cache è il percorso: distingue da sola ENRMVA/lipp.mva dall'ipotetico lipp.mva di root.
        var file = await _cache.GetMvaFileAsync(relative, token => CaricaAsync(relative, token), ct);
        if (file.Testo is null || _stati is null) return file.Carta;   // 404: l'assenza non tocca il ricordo

        // 🔴 U-037 (revisione totale 3): una carta rivista per il ciclo prossimo entrava subito anche nelle release
        // del ciclo in corso. Il testo si confronta con quello ricordato una volta per caricamento, e dentro la
        // cattura di una release si dà quello in vigore al suo ciclo.
        if (!file.Riconciliato)
        {
            await _stati.RiconciliaAsync(relative, file.Testo, ct);
            file.Riconciliato = true;
        }
        if (_cattura?.Cycle is { } ciclo && await _stati.TestoInVigoreAsync(relative, ciclo, ct) is { } inVigore)
            return AuroraSectorfileParser.ParseMva(inVigore);
        return file.Carta;
    }

    private async Task<MvaFile> CaricaAsync(string relative, CancellationToken token)
    {
        var text = await SectorfileRaw.GetTextOrNullAsync(_http, _opt.RawBaseUrl, relative, token);
        if (text is null)
        {
            // 404 = caso normale, non un guasto: 25 APP su 49 non hanno il file, e nel sectorfile è
            // indistinguibile «non serve» da «non l'ha ancora fatto nessuno» (nessuna componente è obbligatoria).
            _log.LogDebug("MRVA: {Path} non presente nel sectorfile.", relative);
            return new MvaFile(null, MvaChart.Empty);
        }

        var chart = AuroraSectorfileParser.ParseMva(text);

        // Un file presente ma illeggibile è l'unico caso che vale un avviso: il parser scarta le righe
        // malformate in silenzio, quindi senza questo log un cambio di formato a monte sparirebbe.
        if (chart.IsEmpty)
            _log.LogWarning("MRVA: {Path} presente ma senza contenuto leggibile (formato .mva cambiato?).", relative);
        else
            _log.LogInformation("MRVA {Path}: {Shapes} tracciati, {Labels} etichette.",
                relative, chart.Shapes.Count, chart.Labels.Count);
        return new MvaFile(text, chart);
    }

    private static string Norm(string code) => code.Trim().ToLowerInvariant();
}
