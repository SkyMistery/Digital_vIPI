using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Infrastructure.Translation;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il motore Azure AI Translator, primario dal 27 agosto 2026 (carta
/// <c>2026-08-27-documenti-bilingue.md</c> §4).
///
/// <para>
/// ⚠️ <b>I due test che valgono più degli altri</b> sono
/// <see cref="Senza_la_regione_Azure_risponde_401_quindi_la_regione_si_manda"/> e
/// <see cref="Il_403_vuol_dire_DUE_cose_e_le_azioni_sono_opposte"/>: sono le due trappole che, sbagliate,
/// non si diagnosticano da sole — mandano a cercare il guasto dalla parte opposta.
/// </para>
/// </summary>
public class AzureTranslationEngineTests
{
    private sealed class SpiaHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _stato;
        private readonly string _corpo;

        public SpiaHandler(HttpStatusCode stato, string corpo = "[]")
        {
            _stato = stato;
            _corpo = corpo;
        }

        public Uri? UltimaUri { get; private set; }
        public string? UltimoCorpoInviato { get; private set; }
        public IReadOnlyDictionary<string, string> Intestazioni { get; private set; } =
            new Dictionary<string, string>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimaUri = request.RequestUri;
            UltimoCorpoInviato = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Intestazioni = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
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

    private static AzureTranslationEngine Motore(HttpMessageHandler h, string? chiave = "chiave-finta", string? regione = "westeurope") =>
        new(new StubFactory(h), Options.Create(new TranslationOptions
        {
            Enabled = true,
            Azure = new AzureOptions { ApiKey = chiave, Region = regione },
        }));

    private static string Risposta(params string[] testi) =>
        "[" + string.Join(",", testi.Select(t => "{\"translations\":[{\"text\":\"" + t + "\",\"to\":\"en\"}]}")) + "]";

    // ---- Le due trappole -----------------------------------------------------------------------------

    [Fact]
    public async Task Senza_la_regione_Azure_risponde_401_quindi_la_regione_si_manda()
    {
        // ⚠️ Su una risorsa regionale o multi-servizio, senza Ocp-Apim-Subscription-Region Azure risponde
        // 401 -- che somiglia a una chiave sbagliata, e manda a rigenerare una chiave che andava benissimo.
        var spia = new SpiaHandler(HttpStatusCode.OK, Risposta("Contact the tower"));
        await Motore(spia).TranslateAsync(new[] { "Contatta la torre" }, "it", "en");

        Assert.Equal("chiave-finta", spia.Intestazioni["Ocp-Apim-Subscription-Key"]);
        Assert.Equal("westeurope", spia.Intestazioni["Ocp-Apim-Subscription-Region"]);
    }

    [Fact]
    public async Task Senza_regione_configurata_l_intestazione_non_si_manda_vuota()
    {
        // Una risorsa globale non la vuole, e mandarla vuota non aiuta nessuno.
        var spia = new SpiaHandler(HttpStatusCode.OK, Risposta("x"));
        await Motore(spia, regione: null).TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.False(spia.Intestazioni.ContainsKey("Ocp-Apim-Subscription-Region"));
    }

    [Fact]
    public async Task Il_403_vuol_dire_DUE_cose_e_le_azioni_sono_opposte()
    {
        // Chiave rifiutata: serve una persona, e passare all'altro motore e' comunque giusto ma il motivo
        // va detto bene.
        var rifiutata = await Motore(new SpiaHandler(HttpStatusCode.Forbidden,
            "{\"error\":{\"code\":403000,\"message\":\"The operation is not allowed\"}}"))
            .TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Equal(TranslationOutcome.AuthFailed, rifiutata.Outcome);

        // Quota finita: non e' un problema di chiave, ed e' esattamente il caso per cui esiste la catena.
        var esaurita = await Motore(new SpiaHandler(HttpStatusCode.Forbidden,
            "{\"error\":{\"code\":403001,\"message\":\"exceeded free quota\"}}"))
            .TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Equal(TranslationOutcome.QuotaExceeded, esaurita.Outcome);
    }

    [Fact]
    public async Task Un_403_col_corpo_illeggibile_si_legge_come_chiave_rifiutata()
    {
        // Si sceglie l'ipotesi che NON consuma l'altro motore. Se fosse davvero quota, il giro dopo lo dira'
        // con un corpo leggibile.
        var esito = await Motore(new SpiaHandler(HttpStatusCode.Forbidden, "non json {{"))
            .TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Equal(TranslationOutcome.AuthFailed, esito.Outcome);
    }

    // ---- Che cosa finisce sul filo -------------------------------------------------------------------

    [Fact]
    public async Task La_richiesta_chiede_il_trattamento_HTML_e_le_lingue_brevi()
    {
        var spia = new SpiaHandler(HttpStatusCode.OK, Risposta("Contact the tower"));
        await Motore(spia).TranslateAsync(new[] { "Contatta la torre" }, "it", "en");

        var url = spia.UltimaUri!.ToString();
        Assert.Contains("api-version=3.0", url);
        Assert.Contains("from=it", url);
        Assert.Contains("to=en", url);
        // textType=html e' il modo in cui Azure lascia stare i segnaposto.
        Assert.Contains("textType=html", url);
        Assert.Contains("\"Text\":\"Contatta la torre\"", spia.UltimoCorpoInviato);
    }

    [Fact]
    public async Task Senza_chiave_il_motore_dice_che_non_e_configurato()
    {
        var motore = Motore(new SpiaHandler(HttpStatusCode.OK), chiave: null);
        Assert.False(motore.IsConfigured);
        var esito = await motore.TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Equal(TranslationOutcome.NotConfigured, esito.Outcome);
        Assert.Equal("azure", esito.Engine);
    }

    // ---- Esiti ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TranslationOutcome.AuthFailed)]
    [InlineData(HttpStatusCode.TooManyRequests, TranslationOutcome.TemporaryFailure)]
    [InlineData(HttpStatusCode.ServiceUnavailable, TranslationOutcome.TemporaryFailure)]
    [InlineData(HttpStatusCode.BadRequest, TranslationOutcome.PermanentFailure)]
    public async Task Ogni_codice_di_stato_ha_il_suo_verdetto(HttpStatusCode stato, TranslationOutcome atteso)
    {
        var esito = await Motore(new SpiaHandler(stato, "{}")).TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Equal(atteso, esito.Outcome);
        Assert.Null(esito.Texts);
    }

    [Fact]
    public async Task Il_successo_torna_i_testi_nell_ordine_chiesto_e_dice_chi_ha_tradotto()
    {
        var esito = await Motore(new SpiaHandler(HttpStatusCode.OK, Risposta("First", "Second")))
            .TranslateAsync(new[] { "Primo", "Secondo" }, "it", "en");

        Assert.Equal(TranslationOutcome.Ok, esito.Outcome);
        Assert.Equal(new[] { "First", "Second" }, esito.Texts);
        Assert.Equal("azure", esito.Engine);
    }

    [Fact]
    public async Task Se_tornano_meno_testi_di_quanti_ne_abbiamo_chiesti_si_butta_tutto()
    {
        var esito = await Motore(new SpiaHandler(HttpStatusCode.OK, Risposta("solo una")))
            .TranslateAsync(new[] { "Uno", "Due" }, "it", "en");
        Assert.Equal(TranslationOutcome.PermanentFailure, esito.Outcome);
        Assert.Contains("attesi 2", esito.Detail);
    }

    [Fact]
    public async Task Una_risposta_che_non_ha_la_forma_attesa_e_un_guasto_definitivo()
    {
        var esito = await Motore(new SpiaHandler(HttpStatusCode.OK, "{\"non\":\"un array\"}"))
            .TranslateAsync(new[] { "Testo" }, "it", "en");
        Assert.Equal(TranslationOutcome.PermanentFailure, esito.Outcome);
    }

    // ---- I segnaposto sopravvivono al giro Azure -------------------------------------------------------

    [Fact]
    public void Azure_puo_normalizzare_il_segnaposto_e_il_ripristino_lo_accetta()
    {
        // ⚠️ In modalita' HTML Azure puo' restituire <x id="0"/> come <x id="0"></x>. Se il ripristino
        // pretendesse la forma esatta, OGNI segmento con un callsign risulterebbe "segnaposto mangiato" e
        // finirebbe fra gli scartati: la traduzione non funzionerebbe mai, e il rapporto darebbe la colpa
        // al motore.
        var protettore = new TextProtector();
        var protetto = protettore.Protect("Contatta LIRF_TWR sulla 118.1");
        Assert.Equal(2, protetto.Tokens.Count);

        var comeLoRendeAzure = "Contact <x id=\"0\"></x> on <x id=\"1\"></x>";
        Assert.True(TextProtector.TryRestore(comeLoRendeAzure, protetto.Tokens, out var tornato));
        Assert.Equal("Contact LIRF_TWR on 118.1", tornato);
    }
}
