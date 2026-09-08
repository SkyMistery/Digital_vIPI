using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// METAR da <b>IVAO</b> (<c>/v2/airports/{icao}/metar</c>, token app): la <b>prima</b> sorgente di scorta
/// quando NOAA non dà il bollettino.
///
/// <para><b>Perché prima di VATSIM</b>, e non è gusto: è la rete che questi documenti servono. Il METAR che
/// IVAO pubblica è quello con cui volano i piloti e lavorano i controllori a cui la vIPI parla — se le due
/// sorgenti divergessero, quella giusta per noi è questa. E le credenziali ci sono già: nessun fornitore
/// nuovo da presidiare.</para>
///
/// <para>⚠️ <b>Il percorso è MINUSCOLO.</b> <c>/v2/airports/LIRF/METAR</c> risponde <b>404</b>,
/// <c>/v2/airports/LIRF/metar</c> risponde <b>200</b> — misurato l'8 settembre 2026 col token vero. La prima
/// prova fu fatta <i>anonima e maiuscola</i>, e il doppio errore fece concludere che l'endpoint non
/// esistesse; il committente ha chiesto di ricontrollare, e aveva ragione. <b>Un 404 su un'API autenticata
/// non dice «non c'è»: dice «non c'è COSÌ».</b></para>
///
/// <para>⚠️ Un ICAO che IVAO non serve torna 404 con <c>«METAR not available for this airport»</c>, e senza
/// credenziali torna 401: tutt'e due diventano <c>null</c>, cioè «passa alla prossima scorta».</para>
///
/// <para>⚠️ <b>Non passa da <c>IvaoHttp</c></b>, che è un typed client transient: chi lo usa è
/// <c>NoaaWeatherClient</c>, che è <b>singleton</b>, e tenersi dentro un typed client vorrebbe dire
/// congelarne l'handler per sempre. Qui bastano il token — <c>IvaoTokenProvider</c> è già singleton — e una
/// <c>HttpClient</c> presa dalla fabbrica a ogni chiamata.</para>
///
/// <para>⚠️ <b>Niente cache</b>: la chiama <c>NoaaWeatherClient</c> dentro la propria fetch, quindi eredita
/// la sua cache per ICAO, la deduplica delle richieste in volo e la ricaduta sull'ultimo valore noto.</para>
/// </summary>
public sealed class IvaoMetarClient : IMetarFallback
{
    private readonly IHttpClientFactory _factory;
    private readonly IvaoTokenProvider _token;
    private readonly IvaoOptions _opt;

    public IvaoMetarClient(IHttpClientFactory factory, IvaoTokenProvider token, IOptions<IvaoOptions> opt)
    {
        _factory = factory;
        _token = token;
        _opt = opt.Value;
    }

    public string Nome => "IVAO";

    public async Task<string?> GetMetarAsync(string icao, CancellationToken ct = default)
    {
        // Senza credenziali non si prova nemmeno: sarebbe un 401 per ogni aeroporto di ogni pagina.
        if (!_token.IsConfigured || string.IsNullOrWhiteSpace(icao)) return null;

        try
        {
            var tok = await _token.GetTokenAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tok)) return null;

            var url = $"{_opt.BaseUrl.TrimEnd('/')}/v2/airports/" +
                      $"{Uri.EscapeDataString(icao.Trim().ToUpperInvariant())}/metar";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok);

            var http = _factory.CreateClient(Weather.NoaaWeatherClient.HttpClientName);
            using var res = await http.SendAsync(req, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;   // 404 «non disponibile», 401 «senza token»: uguale per noi

            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body)) return null;

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var metar = doc.RootElement.TryGetProperty("metar", out var m) ? m.GetString()?.Trim() : null;
            return string.IsNullOrWhiteSpace(metar) ? null : metar;
        }
        catch
        {
            // La scorta non alza MAI: chi la chiama sta già riempiendo un buco, e un'eccezione qui
            // trasformerebbe «niente meteo» in «pagina rotta».
            return null;
        }
    }
}
