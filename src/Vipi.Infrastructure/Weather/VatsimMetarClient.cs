using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Weather;

/// <summary>
/// Sorgente METAR di scorta: <c>metar.vatsim.net</c>. Testo grezzo, nessuna chiave, nessuna registrazione.
///
/// <para><b>Perché questa.</b> Segnalato dal committente l'8 settembre 2026: «ogni tanto NOAA fallisce e non
/// dà il METAR». Le candidate sono state <b>provate</b>, non scelte a memoria:</para>
/// <list type="bullet">
///   <item><c>tgftp.nws.noaa.gov</c> risponde, ma <b>è ancora NOAA</b>: un guasto del fornitore si porta via
///   tutt'e due, e una scorta che cade insieme alla principale non è una scorta.</item>
///   <item><c>api.ivao.aero/v2/airports/{icao}/METAR</c> → <b>404</b>: quell'endpoint non esiste.</item>
///   <item><c>metar.vatsim.net</c> → 200 in 0,3 s, 50 byte di METAR puro. Operatore <b>diverso</b>, ed è il
///   punto.</item>
/// </list>
///
/// <para>⚠️ <b>Un ICAO sconosciuto torna 200 con il corpo VUOTO</b>, non un errore e non una pagina d'errore
/// (misurato con <c>ZZZZ</c>). È il comportamento che serve: «non ce l'ho» si distingue da «sono rotto» senza
/// dover interpretare del testo. Copre anche i campi militari (LIBG risponde) ed è insensibile al maiuscolo.</para>
///
/// <para>⚠️ <b>Niente cache qui</b>, ed è voluto: questa classe la chiama <see cref="NoaaWeatherClient"/>
/// <i>dentro</i> la propria fetch, quindi eredita la sua cache per ICAO, la deduplica delle richieste in volo
/// e la ricaduta sull'ultimo valore noto. Una seconda cache accanto alla prima sarebbe due verità sullo stesso
/// METAR, con due scadenze diverse.</para>
/// </summary>
public sealed class VatsimMetarClient : IMetarFallback
{
    private readonly IHttpClientFactory _factory;
    private readonly WeatherOptions _opt;

    public VatsimMetarClient(IHttpClientFactory factory, IOptions<WeatherOptions> opt)
    {
        _factory = factory;
        _opt = opt.Value;
    }

    public string Nome => _opt.FallbackMetarName;

    public async Task<string?> GetMetarAsync(string icao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(icao) || string.IsNullOrWhiteSpace(_opt.FallbackMetarUrl)) return null;

        try
        {
            var url = string.Format(_opt.FallbackMetarUrl, Uri.EscapeDataString(icao.Trim().ToUpperInvariant()));
            var http = _factory.CreateClient(NoaaWeatherClient.HttpClientName);
            var testo = (await http.GetStringAsync(url, ct).ConfigureAwait(false))?.Trim();

            // ⚠️ Corpo vuoto = «non ce l'ho», ed è la risposta normale per un ICAO che la rete non serve.
            if (string.IsNullOrWhiteSpace(testo)) return null;

            // ⚠️ Una riga sola, e la PRIMA: se un giorno la sorgente cominciasse a rispondere con una pagina
            // d'errore o con più stazioni, prendere tutto vorrebbe dire scrivere quella roba dentro un
            // documento operativo. Un METAR comincia con l'ICAO che si è chiesto: se non comincia così, non
            // è il nostro e non si usa.
            var riga = testo.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return riga.StartsWith(icao.Trim(), StringComparison.OrdinalIgnoreCase) ? riga : null;
        }
        catch
        {
            // La scorta non alza MAI: chi la chiama sta già riempiendo un buco, e un'eccezione qui
            // trasformerebbe «niente meteo» in «pagina rotta».
            return null;
        }
    }
}
