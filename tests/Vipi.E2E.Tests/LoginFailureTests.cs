using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Vipi.Host.Auth;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il guasto del login IVAO: come viene classificato e cosa finisce in pagina.
///
/// <para>Fino al 23 agosto 2026 non esisteva <c>OnRemoteFailure</c>, quindi ogni guasto del giro OIDC usciva
/// come eccezione non gestita su <c>/Error</c> — nessun messaggio per chi legge, nessuna riga per noi. Il
/// motivo serve a due padroni: va nel log <b>e</b> sceglie la frase mostrata. Per questo è un insieme
/// chiuso, ed è quello che questi test tengono chiuso.</para>
///
/// ℹ️ Non c'è un test che chiami davvero l'endpoint: la fabbrica di <see cref="SmokeTests"/> spegne l'auth
/// con una variabile d'ambiente di PROCESSO, e una seconda fabbrica che la riaccende darebbe una corsa fra
/// classi di test eseguite in parallelo. Qui si prova la logica; il montaggio della rotta è una riga.
/// </summary>
public sealed class LoginFailureTests
{
    [Fact]
    public void Errore_del_portale_vince_sul_messaggio_dell_eccezione()
    {
        // Anche con un'eccezione che parla di correlazione: se IVAO ha detto no, la causa è quella.
        var failure = new Exception("Correlation failed.");
        Assert.Equal("portale", VipiStandaloneAuthExtensions.ClassifyRemoteFailure(failure, "access_denied"));
    }

    [Theory]
    [InlineData("Correlation failed.", "correlazione")]
    [InlineData("correlation failed", "correlazione")]
    [InlineData("The oauth state was missing or invalid.", "correlazione")]
    // Misurato sul flusso vero (callback senza cookie di stato, 24-ago-2026): è anche il sintomo di un
    // key-ring perso in produzione, quindi non deve cadere fra gli sconosciuti.
    [InlineData("Unable to unprotect the message.State.", "correlazione")]
    [InlineData("IDX21323: RequireNonce is '[PII is hidden]'.", "nonce")]
    [InlineData("IDX21320: The 'nonce' parameter was not found.", "nonce")]
    [InlineData("Unable to obtain configuration from 'https://api.ivao.aero'", "sconosciuto")]
    public void Il_motivo_si_legge_dal_messaggio_quando_il_portale_tace(string message, string atteso) =>
        Assert.Equal(atteso, VipiStandaloneAuthExtensions.ClassifyRemoteFailure(new Exception(message), null));

    [Fact]
    public void Senza_eccezione_e_senza_errore_il_motivo_resta_sconosciuto() =>
        Assert.Equal("sconosciuto", VipiStandaloneAuthExtensions.ClassifyRemoteFailure(null, null));

    [Fact]
    public void Il_messaggio_annidato_conta_quanto_quello_esterno()
    {
        // L'handler avvolge: il testo che classifica sta nella InnerException. ToString() le comprende.
        var failure = new OpenIdConnectProtocolException(
            "Message contains error", new Exception("IDX21323: RequireNonce"));
        Assert.Equal("nonce", VipiStandaloneAuthExtensions.ClassifyRemoteFailure(failure, null));
    }

    [Theory]
    [InlineData("portale", "portale")]
    [InlineData("correlazione", "correlazione")]
    [InlineData("nonce", "nonce")]
    // Tutto ciò che arriva dall'URL e non è dell'insieme chiuso decade: la pagina non riflette testo altrui.
    [InlineData("<script>alert(1)</script>", "sconosciuto")]
    [InlineData("Correlation failed.", "sconosciuto")]
    [InlineData(null, "sconosciuto")]
    [InlineData("", "sconosciuto")]
    public void Solo_i_motivi_previsti_arrivano_in_pagina(string? motivo, string atteso) =>
        Assert.Equal(atteso, VipiStandaloneAuthExtensions.NormalizeReason(motivo));

    [Fact]
    public void La_pagina_porta_il_ritorno_e_il_tasto_riprova()
    {
        var html = IvaoLoginFailurePage.Build("correlazione", "/services/vsop/lirr/airports?icao=LIRF");

        Assert.Contains("/services/vsop/auth/login?returnUrl=", html);
        // Il ritorno viaggia due volte: dentro il link di login (percent-encoded) e come «continua senza».
        Assert.Contains("%2Fservices%2Fvsop%2Flirr%2Fairports", html);
        Assert.Contains("href=\"/services/vsop/lirr/airports?icao=LIRF\"", html);
        Assert.Contains("correlazione", html);
    }

    [Fact]
    public void Il_ritorno_finisce_in_pagina_codificato_non_com_e()
    {
        // SafeReturn scarterebbe già un ingresso così; qui si prova lo strato dopo, che non deve fidarsi.
        var html = IvaoLoginFailurePage.Build("sconosciuto", "/a\"><script>alert(1)</script>");

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.DoesNotContain("\"><script", html);
    }

    [Fact]
    public void La_pagina_dice_di_riprovare_solo_dove_riprovare_serve()
    {
        // Consenso negato sul portale: rimbalzare sul login non lo aggiusta, e la pagina non lo promette.
        var portale = IvaoLoginFailurePage.Build("portale", "/services/vsop");
        Assert.DoesNotContain("quasi sempre al secondo tentativo", portale);

        // Correlazione/nonce: il secondo tentativo rigenera i cookie di stato, quindi di norma entra.
        var correlazione = IvaoLoginFailurePage.Build("correlazione", "/services/vsop");
        Assert.Contains("quasi sempre al secondo tentativo", correlazione);
    }
}
