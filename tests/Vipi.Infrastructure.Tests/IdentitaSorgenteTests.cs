using System.Text.Json;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'<b>identità</b> che la sorgente manda: l'id numerico di subcenter e postazioni ATC.
///
/// <para>La trappola che questi casi presidiano è che <c>id</c> non vuol dire la stessa cosa su tutti gli
/// endpoint IVAO. Sui subcenter e sulle postazioni è il numero che identifica la riga (1174, 3954) e
/// sopravvive a una rinomina del callsign; su <c>/v2/centers</c> è il <b>codice ACC come stringa</b>
/// ("LIRR"), tanto che <c>IvaoAccClient</c> lo usa come ripiego per costruire il callsign. Un parser
/// tollerante li appiattirebbe entrambi in una stringa e "LIRR" finirebbe a fare da identità.</para>
/// </summary>
public class IdentitaSorgenteTests
{
    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    [Fact]
    public void IdNumerico_e_lIdentita_della_riga()
    {
        var subcenter = Json("""{"id":1174,"centerId":"LIRR","composePosition":"LIRR_NE_CTR"}""");
        Assert.Equal(1174, IvaoHttp.JsonIntId(subcenter, "id"));

        var postazione = Json("""{"id":3954,"airportId":"LIRF","composePosition":"LIRF_DEL"}""");
        Assert.Equal(3954, IvaoHttp.JsonIntId(postazione, "id"));
    }

    [Fact]
    public void IdStringa_dei_center_non_diventa_unIdentita()
    {
        // /v2/centers risponde così: `id` è il codice ACC, non un surrogato numerico.
        var center = Json("""{"military":true,"id":"LIRR","name":"Roma","countryId":"IT"}""");

        Assert.Null(IvaoHttp.JsonIntId(center, "id"));
        Assert.Equal("LIRR", IvaoHttp.JsonId(center, "id"));   // il parser tollerante lo legge ancora, e va bene: lì serve
    }

    [Theory]
    [InlineData("""{"composePosition":"LIRR_NE_CTR"}""")]        // campo assente
    [InlineData("""{"id":null}""")]                              // esplicitamente nullo
    [InlineData("""{"id":"3954"}""")]                            // numero travestito da stringa
    [InlineData("""{"id":12345678901}""")]                       // non entra in un int
    [InlineData("""{"id":3.5}""")]                               // non è un intero
    public void SenzaUnIdNumericoVero_resta_null(string payload) =>
        Assert.Null(IvaoHttp.JsonIntId(Json(payload), "id"));
}
