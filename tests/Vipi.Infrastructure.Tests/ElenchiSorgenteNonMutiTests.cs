using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-002 (revisione totale 3): gli ELENCHI della sorgente distinguono «non c'è niente» da «non ho letto».
///
/// <para><b>Il difetto.</b> <c>IvaoHttp.GetJsonAsync</c>/<c>GetStringAsync</c> trasformano ogni non-2xx in
/// <c>null</c>, e i client ne facevano un elenco vuoto: postazioni d'aeroporto, piste, subcenter di un ACC. Un token
/// senza lo scope giusto (403), un token revocato (401), un 5xx per due notti erano «nessuna postazione»: il giro
/// dei settori timbrava RIUSCITO e dopo due notti cadeva la D8 dell'eliminazione («la sorgente la manda ancora»)
/// su ogni settore d'aeroporto. È il gemello di T-006 che quella correzione non copriva: T-006 fa risalire le
/// eccezioni dei giri, ma qui un'eccezione non c'era.</para>
///
/// <para>La regola: <b>404 è vuoto legittimo</b> (lo scalo non ha postazioni, l'ACC non ha subcenter), ogni altro
/// non-2xx solleva con lo status. I DETTAGLI per voce restano best-effort: un dettaglio non letto tiene i dati
/// della lista, e questo lo presidiano già i test di T-007.</para>
/// </summary>
public class ElenchiSorgenteNonMutiTests
{
    private const string Postazioni = "/v2/airports/LIRN/ATCPositions";
    private const string Piste = "/v2/airports/LIRN/runways";
    private const string Subcenter = "/v2/centers/LIRR/subcenters";

    public static TheoryData<HttpStatusCode> Guasti => new()
    {
        HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable,
    };

    [Theory]
    [MemberData(nameof(Guasti))]
    public async Task Postazioni_d_aeroporto_non_lette_sollevano(HttpStatusCode status)
    {
        var client = Dettagli(new() { [Postazioni] = (status, "") });
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAtcPositionsAsync("LIRN"));
        Assert.Equal(status, ex.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Guasti))]
    public async Task Piste_non_lette_sollevano(HttpStatusCode status)
    {
        var client = Dettagli(new() { [Piste] = (status, "") });
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetRunwaysAsync("LIRN"));
        Assert.Equal(status, ex.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Guasti))]
    public async Task Subcenter_di_un_ACC_non_letti_sollevano(HttpStatusCode status)
    {
        var client = Acc(new() { [Subcenter] = (status, "") });
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetSubcentersAsync("LIRR"));
        Assert.Equal(status, ex.StatusCode);
    }

    /// <summary>Il 404 resta quello che era: la sorgente dice che lì non c'è niente, ed è una risposta.</summary>
    [Fact]
    public async Task Un_404_e_un_elenco_vuoto_come_prima()
    {
        var vuoto = new Dictionary<string, (HttpStatusCode, string)>();
        Assert.Empty(await Dettagli(vuoto).GetAtcPositionsAsync("LIRN"));
        Assert.Empty(await Dettagli(vuoto).GetRunwaysAsync("LIRN"));
        Assert.Empty(await Acc(vuoto).GetSubcentersAsync("LIRR"));
    }

    /// <summary>Un elenco letto passa com'è, e il dettaglio che non risponde non lo ferma (best-effort per voce).</summary>
    [Fact]
    public async Task Un_elenco_letto_passa_e_il_dettaglio_mancante_non_lo_ferma()
    {
        var client = Acc(new()
        {
            [Subcenter] = (HttpStatusCode.OK, """[{"id":1174,"composePosition":"LIRR_NE_CTR","centerId":"LIRR"}]"""),
            ["/v2/subcenters/LIRR_NE_CTR"] = (HttpStatusCode.TooManyRequests, ""),
        });

        var subs = await client.GetSubcentersAsync("LIRR");

        Assert.Equal("LIRR_NE_CTR", Assert.Single(subs).ComposePosition);
    }

    private static IvaoOptions Opzioni() => new() { ClientId = "prova", ClientSecret = "x" };

    private static IvaoHttp Http(Dictionary<string, (HttpStatusCode, string)> risposte)
    {
        var opt = Options.Create(Opzioni());
        return new IvaoHttp(new HttpClient(new Centralino(risposte)), new IvaoTokenProvider(new FabbricaConToken(), opt), opt);
    }

    private static IvaoAirportDetailClient Dettagli(Dictionary<string, (HttpStatusCode, string)> risposte) =>
        new(Http(risposte), Options.Create(Opzioni()));

    private static IvaoAccClient Acc(Dictionary<string, (HttpStatusCode, string)> risposte) =>
        new(Http(risposte), Options.Create(Opzioni()));

    private sealed class Centralino(Dictionary<string, (HttpStatusCode Status, string Body)> r) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var (status, body) = r.TryGetValue(req.RequestUri!.AbsolutePath, out var hit) ? hit : (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class FabbricaConToken : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Stampella());

        private sealed class Stampella : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"finto","expires_in":3600}""", Encoding.UTF8, "application/json"),
                });
        }
    }
}
