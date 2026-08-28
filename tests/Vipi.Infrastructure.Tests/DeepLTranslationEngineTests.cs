using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Infrastructure.Translation;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il motore DeepL (carta <c>2026-08-27-documenti-bilingue.md</c> §4): che cosa mette sul filo, e come
/// legge le risposte che non sono un successo.
///
/// <para>
/// ⚠️ <b>Il verdetto a più valori è il punto di questi test.</b> Su <c>IvaoHttp</c> ogni risposta non
/// riuscita diventava <c>null</c>, e «chiave scaduta», «quota finita» e «servizio giù per due minuti»
/// erano indistinguibili in fondo al log. Qui sono tre esiti perché sono tre azioni diverse: chiamare una
/// persona, aspettare il periodo nuovo, riprovare fra poco.
/// </para>
/// </summary>
public class DeepLTranslationEngineTests
{
    /// <summary>Handler che registra l'ultima richiesta e risponde ciò che gli si dice.</summary>
    private sealed class SpiaHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _stato;
        private readonly string _corpo;

        public SpiaHandler(HttpStatusCode stato, string corpo = "")
        {
            _stato = stato;
            _corpo = corpo;
        }

        public string? UltimoCorpoInviato { get; private set; }
        public string? UltimaAutorizzazione { get; private set; }
        public Uri? UltimaUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimaUri = request.RequestUri;
            UltimaAutorizzazione = request.Headers.TryGetValues("Authorization", out var v) ? string.Join("", v) : null;
            UltimoCorpoInviato = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_stato)
            {
                Content = new StringContent(_corpo, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _h;
        public StubFactory(HttpMessageHandler h) => _h = h;
        public HttpClient CreateClient(string name) => new(_h, disposeHandler: false);
    }

    private static DeepLTranslationEngine Motore(HttpMessageHandler h, string? chiave = "chiave-finta:fx", string? glossario = null) =>
        new(new StubFactory(h), Options.Create(new TranslationOptions
        {
            Enabled = true,
            DeepL = new DeepLOptions { ApiKey = chiave, GlossaryId = glossario },
        }));

    private static string Risposta(params string[] testi) =>
        "{\"translations\":[" + string.Join(",", testi.Select(t => "{\"text\":\"" + t + "\"}")) + "]}";

    // ---- Configurazione ------------------------------------------------------------------------------

    [Fact]
    public async Task Senza_chiave_il_motore_dice_che_non_e_configurato()
    {
        // Non e' un errore: e' un sito che non traduce, e deve continuare a funzionare.
        var motore = Motore(new SpiaHandler(HttpStatusCode.OK), chiave: null);
        Assert.False(motore.IsConfigured);
        var esito = await motore.TranslateAsync(new[] { "Contatta la torre" }, "it", "en");
        Assert.Equal(TranslationOutcome.NotConfigured, esito.Outcome);
    }

    [Fact]
    public async Task Una_chiave_del_piano_gratuito_va_al_server_gratuito()
    {
        // ⚠️ Puntare all'altro server risponde 403, che somiglia a una chiave scaduta e manda a cercare il
        // guasto dalla parte opposta. La chiave lo dice da se': quelle gratuite finiscono in «:fx».
        var spia = new SpiaHandler(HttpStatusCode.OK, Risposta("Contact the tower"));
        await Motore(spia, "abc:fx").TranslateAsync(new[] { "Contatta la torre" }, "it", "en");
        Assert.Equal("api-free.deepl.com", spia.UltimaUri!.Host);

        var spia2 = new SpiaHandler(HttpStatusCode.OK, Risposta("Contact the tower"));
        await Motore(spia2, "abc").TranslateAsync(new[] { "Contatta la torre" }, "it", "en");
        Assert.Equal("api.deepl.com", spia2.UltimaUri!.Host);
    }

    // ---- Che cosa finisce sul filo -------------------------------------------------------------------

    [Fact]
    public async Task La_richiesta_porta_la_gestione_dei_tag_e_il_bersaglio_giusto()
    {
        var spia = new SpiaHandler(HttpStatusCode.OK, Risposta("Contact the tower"));
        await Motore(spia).TranslateAsync(new[] { "Contatta la torre" }, "it", "en");

        Assert.Contains("\"tag_handling\":\"xml\"", spia.UltimoCorpoInviato);
        // ⚠️ «g» accanto a «x»: è il modo in cui DeepL onora il glossario di fraseologia. Toglierlo non
        // romperebbe il documento — la resa la rimette il ripristino — ma si pagherebbero caratteri
        // per tradurre una frase che poi si butta, e il conto è l'unico posto in cui si vedrebbe.
        Assert.Contains("\"ignore_tags\":[\"x\",\"g\"]", spia.UltimoCorpoInviato);
        Assert.Contains("\"source_lang\":\"IT\"", spia.UltimoCorpoInviato);
        // «EN» secco e' deprecato come bersaglio, e l'inglese aeronautico e' quello britannico.
        Assert.Contains("\"target_lang\":\"EN-GB\"", spia.UltimoCorpoInviato);
        Assert.Equal("DeepL-Auth-Key chiave-finta:fx", spia.UltimaAutorizzazione);
    }

    [Fact]
    public async Task Senza_glossario_il_campo_non_si_manda_nemmeno_vuoto()
    {
        var spia = new SpiaHandler(HttpStatusCode.OK, Risposta("x"));
        await Motore(spia).TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.DoesNotContain("glossary_id", spia.UltimoCorpoInviato);

        var conGlossario = new SpiaHandler(HttpStatusCode.OK, Risposta("x"));
        await Motore(conGlossario, glossario: "g-123").TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Contains("\"glossary_id\":\"g-123\"", conGlossario.UltimoCorpoInviato);
    }

    // ---- La trappola dell'XML -------------------------------------------------------------------------

    [Theory]
    [InlineData("Roma & Milano", "Roma &amp; Milano")]
    [InlineData("traffico < FL100", "traffico &lt; FL100")]
    [InlineData("a > b", "a &gt; b")]
    public void I_caratteri_che_romperebbero_l_XML_si_scappano(string dentro, string atteso) =>
        Assert.Equal(atteso, DeepLTranslationEngine.ScappaTenendoISegnaposto(dentro));

    [Fact]
    public void Il_segnaposto_NON_si_scappa_o_smette_di_essere_un_tag()
    {
        // ⚠️ E' l'ordine l'unica cosa che conta: scappare tutto e poi rimettere i segnaposto, o scappare
        // prima, trasformerebbe «<x id="0"/>» in «&lt;x id="0"/&gt;» -- e il motore lo tradurrebbe come
        // testo invece di lasciarlo stare. Che e' il modo esatto in cui un callsign sparisce da una frase.
        const string protetto = "Contatta <x id=\"0\"/> sulla <x id=\"1\"/> & riporta";
        var scappato = DeepLTranslationEngine.ScappaTenendoISegnaposto(protetto);
        Assert.Contains("<x id=\"0\"/>", scappato);
        Assert.Contains("<x id=\"1\"/>", scappato);
        Assert.Contains("&amp;", scappato);
        Assert.DoesNotContain("&lt;x", scappato);
    }

    [Fact]
    public void Anche_il_segnaposto_del_GLOSSARIO_resta_un_tag()
    {
        // Il segnaposto del glossario porta un attributo (`translate="no"`) e una lettera diversa. Una
        // regola che pretendesse «<x» e nessun attributo lo scapperebbe: DeepL lo tradurrebbe come testo, e
        // la formula si pagherebbe per niente a ogni giro.
        const string protetto = "Poi <g id=\"0\" translate=\"no\">riporta sottovento</g> & attendi";
        var scappato = DeepLTranslationEngine.ScappaTenendoISegnaposto(protetto);

        Assert.Contains("<g id=\"0\" translate=\"no\">riporta sottovento</g>", scappato);
        Assert.Contains("&amp;", scappato);
        Assert.DoesNotContain("&lt;g", scappato);
    }

    [Fact]
    public void La_fuga_si_disfa_nell_ordine_giusto()
    {
        // «&amp;» per ultimo, o «&amp;lt;» tornerebbe «<» invece di «&lt;».
        Assert.Equal("Roma & Milano", DeepLTranslationEngine.Rientra("Roma &amp; Milano"));
        Assert.Equal("a < b > c", DeepLTranslationEngine.Rientra("a &lt; b &gt; c"));
    }

    // ---- Gli esiti che non sono un successo ------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, TranslationOutcome.AuthFailed)]
    [InlineData(HttpStatusCode.Unauthorized, TranslationOutcome.AuthFailed)]
    [InlineData((HttpStatusCode)456, TranslationOutcome.QuotaExceeded)]
    [InlineData(HttpStatusCode.TooManyRequests, TranslationOutcome.TemporaryFailure)]
    [InlineData(HttpStatusCode.ServiceUnavailable, TranslationOutcome.TemporaryFailure)]
    [InlineData(HttpStatusCode.BadRequest, TranslationOutcome.PermanentFailure)]
    public async Task Ogni_codice_di_stato_ha_il_suo_verdetto(HttpStatusCode stato, TranslationOutcome atteso)
    {
        var esito = await Motore(new SpiaHandler(stato)).TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Equal(atteso, esito.Outcome);
        Assert.Null(esito.Texts);
    }

    [Fact]
    public async Task Il_dettaglio_non_contiene_mai_la_chiave()
    {
        var esito = await Motore(new SpiaHandler(HttpStatusCode.Forbidden)).TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.DoesNotContain("chiave-finta", esito.Detail ?? "");
    }

    [Fact]
    public async Task Se_tornano_meno_testi_di_quanti_ne_abbiamo_chiesti_si_butta_tutto()
    {
        // ⚠️ Il contratto e' «uno per ingresso, nello stesso ordine»: chi chiama riaccoppia per POSIZIONE.
        // Aggiustare a naso accoppierebbe la traduzione di una frase con l'IMPRONTA DI UN'ALTRA, e la
        // memoria resterebbe sbagliata per sempre -- su ogni documento che contiene quella frase.
        var spia = new SpiaHandler(HttpStatusCode.OK, Risposta("solo una"));
        var esito = await Motore(spia).TranslateAsync(new[] { "Uno", "Due" }, "it", "en");
        Assert.Equal(TranslationOutcome.PermanentFailure, esito.Outcome);
        Assert.Contains("attesi 2", esito.Detail);
    }

    [Fact]
    public async Task Una_risposta_illeggibile_e_un_guasto_definitivo()
    {
        var esito = await Motore(new SpiaHandler(HttpStatusCode.OK, "non json {{"))
            .TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Equal(TranslationOutcome.PermanentFailure, esito.Outcome);
    }

    [Fact]
    public async Task Il_successo_torna_i_testi_nell_ordine_chiesto()
    {
        var spia = new SpiaHandler(HttpStatusCode.OK, Risposta("First", "Second"));
        var esito = await Motore(spia).TranslateAsync(new[] { "Primo", "Secondo" }, "it", "en");
        Assert.Equal(TranslationOutcome.Ok, esito.Outcome);
        Assert.Equal(new[] { "First", "Second" }, esito.Texts);
    }

    [Fact]
    public async Task Un_elenco_vuoto_non_tocca_la_rete()
    {
        var spia = new SpiaHandler(HttpStatusCode.InternalServerError);
        var esito = await Motore(spia).TranslateAsync(Array.Empty<string>(), "it", "en");
        Assert.Equal(TranslationOutcome.Ok, esito.Outcome);
        Assert.Null(spia.UltimaUri);
    }
}
