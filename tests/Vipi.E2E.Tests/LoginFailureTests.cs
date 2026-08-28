using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Vipi.Application.Tests;
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
        // ⚠️ La cultura si FISSA: da quando la pagina parla la lingua di chi legge, un test che asserisce
        // l'italiano senza fissarla passa in Italia e cade su una macchina inglese.
        using var _ = CulturaDiProva.Italiana();

        // Consenso negato sul portale: rimbalzare sul login non lo aggiusta, e la pagina non lo promette.
        var portale = IvaoLoginFailurePage.Build("portale", "/services/vsop");
        Assert.DoesNotContain("quasi sempre al secondo tentativo", portale);

        // Correlazione/nonce: il secondo tentativo rigenera i cookie di stato, quindi di norma entra.
        var correlazione = IvaoLoginFailurePage.Build("correlazione", "/services/vsop");
        Assert.Contains("quasi sempre al secondo tentativo", correlazione);
    }

    // ---- La lingua ------------------------------------------------------------------------------------
    //
    // ⚠️ Fino al 28 agosto 2026 questa pagina era `lang="it"` con dentro solo italiano, più una riga
    // inglese in grigio in fondo. È la pagina che un lettore inglese vede PROPRIO QUANDO qualcosa si è
    // rotto, cioè nel momento peggiore per non capire che cosa c'è scritto — e la carta della lingua non la
    // dichiarava fra le eccezioni: le eccezioni dichiarate sono log e diagnostica, che un utente non legge.

    [Fact]
    public void In_inglese_la_pagina_e_INGLESE_e_lo_dichiara()
    {
        using var _ = CulturaDiProva.Inglese();
        var html = IvaoLoginFailurePage.Build("correlazione", "/services/vsop");

        Assert.Contains("<html lang=\"en\">", html);
        Assert.Contains("The sign-in expired along the way", html);
        Assert.Contains("the second attempt almost always gets through", html);
        // ⚠️ Sottostringhe SOLO ASCII: titolo e spiegazione passano da HtmlEncoder, che rende in entità
        // numeriche sia l'apostrofo sia le lettere accentate. Cercare «L’accesso è» non troverebbe niente
        // nemmeno quando c'è — un test verde che non guarda più niente.
        Assert.DoesNotContain("scaduto durante il percorso", html);
    }

    [Fact]
    public void In_italiano_la_pagina_e_ITALIANA_e_lo_dichiara()
    {
        using var _ = CulturaDiProva.Italiana();
        var html = IvaoLoginFailurePage.Build("correlazione", "/services/vsop");

        Assert.Contains("<html lang=\"it\">", html);
        Assert.Contains("scaduto durante il percorso", html);
        Assert.DoesNotContain("The sign-in expired along the way", html);
    }

    [Fact]
    public void Una_lingua_che_non_serviamo_ricade_sull_italiano_e_lo_DICHIARA()
    {
        // ⚠️ Il ripiego dev'essere coerente: `lang` deve dire la lingua che c'è DAVVERO nella pagina.
        // A un lettore di schermo, o al traduttore automatico del browser, quella riga è l'unica cosa che
        // dice in che lingua è scritta — `lang="de"` con dentro l'italiano è peggio di niente.
        using var _ = CulturaDiProva.Tedesca();
        var html = IvaoLoginFailurePage.Build("correlazione", "/services/vsop");

        Assert.Contains("<html lang=\"it\">", html);
        Assert.Contains("scaduto durante il percorso", html);
    }

    [Theory]
    [InlineData("portale")]
    [InlineData("correlazione")]
    [InlineData("nonce")]
    [InlineData("sconosciuto")]
    public void Nessun_motivo_lascia_pezzi_di_italiano_nella_pagina_inglese(string motivo)
    {
        // I motivi sono quattro e ognuno porta titolo, spiegazione e rimedio suoi: tradurne tre su quattro
        // darebbe una pagina che è inglese finché non capita proprio quel guasto.
        using var _ = CulturaDiProva.Inglese();
        var html = IvaoLoginFailurePage.Build(motivo, "/services/vsop");

        Assert.Contains("<html lang=\"en\">", html);
        // Spie italiane e SOLO ASCII, scelte fra quelle che l'inglese non contiene: «server.» non andava
        // bene — la frase inglese finisce «recorded on the server.» e la spia scattava sul suo bersaglio.
        foreach (var spia in new[] { "Riprova", "consenso", "questo codice", "scaduto", "Continua senza" })
            Assert.DoesNotContain(spia, html, StringComparison.Ordinal);
    }
}
