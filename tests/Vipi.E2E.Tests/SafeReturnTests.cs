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
    [InlineData("/vsop", "/vsop")]
    [InlineData("/", "/")]
    [InlineData("/vsop/lirr/airports?icao=LIRF", "/vsop/lirr/airports?icao=LIRF")]
    [InlineData("/vsop/live/lirr_ctr#now", "/vsop/live/lirr_ctr#now")]
    // Assente o vuoto: ripiego.
    [InlineData(null, "/vsop")]
    [InlineData("", "/vsop")]
    // Fuori sito, nelle sue forme.
    [InlineData("//evil.example", "/vsop")]
    [InlineData("/\\evil.example", "/vsop")]          // ← il caso che passava
    [InlineData("\\\\evil.example", "/vsop")]
    [InlineData("https://evil.example", "/vsop")]
    [InlineData("http://evil.example", "/vsop")]
    [InlineData("//evil.example/vsop", "/vsop")]
    // Schemi che non sono navigazione.
    [InlineData("javascript:alert(1)", "/vsop")]
    [InlineData("data:text/html,<script>alert(1)</script>", "/vsop")]
    // Response splitting: un a-capo dentro un header Location.
    [InlineData("/vsop\r\nSet-Cookie: a=b", "/vsop")]
    [InlineData("/vsop\nLocation: https://evil.example", "/vsop")]
    public void Solo_i_percorsi_di_questo_sito_sopravvivono(string? ingresso, string atteso) =>
        Assert.Equal(atteso, VipiStandaloneAuthExtensions.SafeReturn(ingresso));
}
