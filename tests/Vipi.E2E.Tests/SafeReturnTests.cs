using Vipi.Host.Auth;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// <c>?returnUrl=</c> del login: l'unico parametro che un estraneo controlla su un endpoint che, subito
/// dopo, autentica davvero l'utente. Un salto fuori sito da lì è phishing con il primo passo autentico.
///
/// <para>La versione precedente si fermava a «comincia per <c>/</c> e non per <c>//</c>» e lasciava passare
/// <c>/\evil.com</c>: i browser normalizzano la barra rovescia <b>prima</b> di risolvere l'URL. È il caso
/// che dà il nome a questo file.</para>
/// </summary>
public sealed class SafeReturnTests
{
    [Theory]
    // Percorsi legittimi: si conservano tali e quali, query e frammento compresi.
    [InlineData("/services/vsop", "/services/vsop")]
    [InlineData("/", "/")]
    [InlineData("/services/vsop/lirr/airports?icao=LIRF", "/services/vsop/lirr/airports?icao=LIRF")]
    [InlineData("/services/vsop/live/lirr_ctr#now", "/services/vsop/live/lirr_ctr#now")]
    // Assente o vuoto: ripiego.
    [InlineData(null, "/services/vsop")]
    [InlineData("", "/services/vsop")]
    // Fuori sito, nelle sue forme.
    [InlineData("//evil.example", "/services/vsop")]
    [InlineData("/\\evil.example", "/services/vsop")]          // ← il caso che passava
    [InlineData("\\\\evil.example", "/services/vsop")]
    [InlineData("https://evil.example", "/services/vsop")]
    [InlineData("http://evil.example", "/services/vsop")]
    [InlineData("//evil.example/services/vsop", "/services/vsop")]
    // Schemi che non sono navigazione.
    [InlineData("javascript:alert(1)", "/services/vsop")]
    [InlineData("data:text/html,<script>alert(1)</script>", "/services/vsop")]
    // Response splitting: un a-capo dentro un header Location.
    [InlineData("/services/vsop\r\nSet-Cookie: a=b", "/services/vsop")]
    [InlineData("/services/vsop\nLocation: https://evil.example", "/services/vsop")]
    public void Solo_i_percorsi_di_questo_sito_sopravvivono(string? ingresso, string atteso) =>
        Assert.Equal(atteso, VipiStandaloneAuthExtensions.SafeReturn(ingresso));
}
