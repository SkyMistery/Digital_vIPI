using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Vipi.Host.Auth;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// <b>Il giro di login IVAO per intero</b>, contro un IVAO finto che vive dentro il test: <c>/auth/login</c>, il
/// ritorno su <c>/signin-oidc</c>, lo scambio del code e la userinfo.
///
/// <para>🔴 <b>Perché (28 settembre 2026).</b> In produzione, al primo login dopo un logout: «The sign-in expired
/// along the way», motivo <c>nonce</c> — IDX21323 «Nonce was null». Il cookie di correlazione era tornato (lo stato
/// del giro recuperato), quello del nonce no; una sola <c>/auth/login</c> prima del ritorno, quindi non il doppio
/// avvio del 18 settembre. Il nonce che l'handler manda a IVAO viaggia anche dentro lo <c>state</c>, cifrato e
/// legato al browser dal cookie di correlazione: se il suo cookie si perde per strada si recupera da lì, ma solo se
/// l'id_token porta <b>quel</b> nonce.</para>
///
/// <para>ℹ️ Un host minimo, non la fabbrica di <see cref="SmokeTests"/>: quella spegne l'auth con una variabile
/// d'ambiente di processo, e riaccenderla da un'altra classe darebbe una corsa fra test paralleli. Qui la
/// configurazione in memoria arriva PRIMA di <c>AddVipiStandaloneAuth</c> e vince sulle variabili.</para>
/// </summary>
public sealed class NonceDelLoginTests
{
    private const string PrefissoNonce = ".AspNetCore.OpenIdConnect.Nonce.";
    private const string PrefissoCorrelazione = ".AspNetCore.Correlation.";
    private const string NonceEstraneo = "638000000000000000.QWx0cm8gZ2lybw";

    [Fact]
    public async Task Con_tutti_i_cookie_il_login_riesce()
    {
        await using var giro = await Giro.AvviaAsync();
        var andata = await giro.AndataAsync("/chi");

        var ritorno = await giro.RitornoAsync(andata, andata.Cookie);

        Assert.Equal("/chi", ritorno.Destinazione);
        Assert.True(ritorno.Dentro, "vipi.auth non emesso");
    }

    /// <summary>🔴 Il guasto del 28 settembre: il cookie del nonce non torna, lo stato sì.</summary>
    [Fact]
    public async Task Senza_il_cookie_del_nonce_il_login_riesce_col_nonce_dello_stato()
    {
        await using var giro = await Giro.AvviaAsync();
        var andata = await giro.AndataAsync("/chi");

        var ritorno = await giro.RitornoAsync(andata, andata.Cookie.Where(c => !c.StartsWith(PrefissoNonce)));

        Assert.Equal("/chi", ritorno.Destinazione);
        Assert.True(ritorno.Dentro, "vipi.auth non emesso");
    }

    /// <summary>
    /// Il recupero non è una porta aperta: un id_token con un nonce che NON è quello mandato in questo giro resta
    /// fuori, col cookie o senza. È esattamente il replay da cui il nonce difende. Dal 29 settembre il primo guasto
    /// rifà il giro una volta (il consenso di IVAO che perde il nonce); se anche il secondo torna con un nonce
    /// estraneo, si va alla pagina e basta.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Un_id_token_con_un_altro_nonce_resta_fuori(bool conCookieDelNonce)
    {
        await using var giro = await Giro.AvviaAsync();
        var andata = await giro.AndataAsync("/chi");
        giro.Ivao.NonceDaMettere = NonceEstraneo;

        var cookie = conCookieDelNonce ? andata.Cookie : andata.Cookie.Where(c => !c.StartsWith(PrefissoNonce));
        var primo = await giro.RitornoAsync(andata, cookie);
        var seconda = giro.SecondaAndata(primo);
        giro.Ivao.NonceDaMettere = NonceEstraneo;

        var secondo = await giro.RitornoAsync(seconda, seconda.Cookie);

        Assert.StartsWith(VipiStandaloneAuthExtensions.LoginFailedPath + "?motivo=nonce", secondo.Destinazione);
        Assert.False(secondo.Dentro);
    }

    /// <summary>
    /// 🔴 Il guasto del 29 settembre (e del 28 sull'hub): primo accesso di un membro, IVAO passa dal consenso e
    /// l'id_token torna con un nonce non nostro. Il sito rifà il giro da solo, con state e nonce nuovi e lo stesso
    /// ritorno, e il secondo giro entra: il membro non vede la pagina d'errore.
    /// </summary>
    [Fact]
    public async Task Il_consenso_che_perde_il_nonce_riparte_una_volta_ed_entra()
    {
        await using var giro = await Giro.AvviaAsync();
        var andata = await giro.AndataAsync("/chi");
        giro.Ivao.NonceDaMettere = NonceEstraneo;

        var primo = await giro.RitornoAsync(andata, andata.Cookie);
        Assert.False(primo.Dentro);
        var seconda = giro.SecondaAndata(primo);
        Assert.NotEqual(andata.State, seconda.State);
        Assert.NotEqual(andata.Nonce, seconda.Nonce);

        var secondo = await giro.RitornoAsync(seconda, seconda.Cookie);

        Assert.Equal("/chi", secondo.Destinazione);
        Assert.True(secondo.Dentro, "vipi.auth non emesso al secondo giro");
        // Il segno del secondo giro serve al giro, non alla sessione.
        var proprieta = await giro.ProprietaDellaSessioneAsync(secondo.CookieDiSessione!);
        Assert.DoesNotContain(VipiStandaloneAuthExtensions.SecondoGiroChiave, proprieta.Keys);
        // E resta persistente come il giro normale: il cookie dura sette giorni anche per chi è entrato così.
        Assert.True(proprieta.ContainsKey(".persistent"), "sessione non persistente dopo il secondo giro");
    }

    [Theory]
    [InlineData("nonce", true, false, true)]
    [InlineData("nonce", true, true, false)]     // già al secondo giro: niente anello
    [InlineData("nonce", false, false, false)]   // stato illeggibile: né ritorno né segno
    [InlineData("correlazione", true, false, false)]
    [InlineData("portale", true, false, false)]
    [InlineData("sconosciuto", true, false, false)]
    public void Riparte_solo_il_nonce_al_primo_giro_con_lo_stato(string motivo, bool stato, bool secondo, bool riparte) =>
        Assert.Equal(riparte, VipiStandaloneAuthExtensions.DeveRipartire(motivo, stato, secondo));

    /// <summary>Senza il cookie di correlazione lo stato non vale niente, e con lui il nonce che porta.</summary>
    [Fact]
    public async Task Senza_il_cookie_di_correlazione_resta_fuori()
    {
        await using var giro = await Giro.AvviaAsync();
        var andata = await giro.AndataAsync("/chi");

        var ritorno = await giro.RitornoAsync(andata,
            andata.Cookie.Where(c => !c.StartsWith(PrefissoNonce) && !c.StartsWith(PrefissoCorrelazione)));

        Assert.StartsWith(VipiStandaloneAuthExtensions.LoginFailedPath + "?motivo=correlazione", ritorno.Destinazione);
        Assert.False(ritorno.Dentro);
    }

    /// <summary>
    /// Il nonce viaggia nello stato solo per il giro: non deve finire nel cookie di sessione, che accompagna ogni
    /// richiesta per sette giorni.
    /// </summary>
    [Fact]
    public async Task Il_nonce_dello_stato_non_finisce_nel_cookie_di_sessione()
    {
        await using var giro = await Giro.AvviaAsync();
        var andata = await giro.AndataAsync("/chi");
        var ritorno = await giro.RitornoAsync(andata, andata.Cookie.Where(c => !c.StartsWith(PrefissoNonce)));

        var proprieta = await giro.ProprietaDellaSessioneAsync(ritorno.CookieDiSessione!);

        Assert.DoesNotContain(proprieta.Keys, k => k.Contains("nonce", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Prima del 28 settembre «cookie perso» e «IVAO rimanda un altro nonce» davano lo stesso IDX21323. La diagnosi
    /// li separa, e si recupera solo nel primo caso.
    /// </summary>
    [Theory]
    [InlineData(false, 1, "n1", "n1", true, "Cookie del nonce: non trovato (1 in richiesta); token col nonce mandato.")]
    [InlineData(false, 0, "n1", "n2", false, "Cookie del nonce: non trovato (0 in richiesta); token con un nonce DIVERSO da quello mandato.")]
    [InlineData(true, 1, "n1", "n1", false, "Cookie del nonce: trovato (1 in richiesta); token col nonce mandato.")]
    [InlineData(false, 0, null, "n1", false, "Cookie del nonce: non trovato (0 in richiesta); stato senza nonce.")]
    [InlineData(false, 0, "n1", null, false, "Cookie del nonce: non trovato (0 in richiesta); token senza nonce.")]
    public void La_diagnosi_separa_cookie_perso_e_nonce_diverso(
        bool cookieTrovato, int inRichiesta, string? mandato, string? ricevuto, bool recupera, string diagnosi)
    {
        var esito = NonceNelloStato.Valuta(cookieTrovato, inRichiesta, mandato, ricevuto);
        Assert.Equal(recupera, esito.Recupera);
        Assert.Equal(diagnosi, esito.Diagnosi);
    }

    [Fact]
    public void La_voce_del_registro_porta_la_diagnosi_del_nonce()
    {
        var con = Vipi.Host.DiagnosticaErrori.VoceDiLogin("nonce", "nessuno", true, false, "/services", null, null,
            "Cookie del nonce: non trovato (0 in richiesta); token con un nonce DIVERSO da quello mandato.");
        var senza = Vipi.Host.DiagnosticaErrori.VoceDiLogin("correlazione", "nessuno", false, false, "/services", null, null);

        Assert.Contains("Nonce ...................... Cookie del nonce: non trovato", con);
        Assert.Contains("Nonce ...................... giro fermo prima del token", senza);
    }

    // ------------------------------------------------------------------------------------------------------------

    private sealed record Andata(string State, string Nonce, IReadOnlyList<string> Cookie);

    private sealed record Ritorno(string Destinazione, string? CookieDiSessione, IReadOnlyList<string> Cookie)
    {
        public bool Dentro => CookieDiSessione is not null;
    }

    /// <summary>Un host col solo modulo di login, e il client che fa da browser (cookie a mano, niente redirect).</summary>
    private sealed class Giro : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly HttpClient _browser;
        public FintoIvao Ivao { get; }

        private Giro(WebApplication app, FintoIvao ivao)
        {
            _app = app;
            Ivao = ivao;
            _browser = app.GetTestServer().CreateClient();
            _browser.BaseAddress = new Uri("https://localhost");
        }

        public static async Task<Giro> AvviaAsync()
        {
            var ivao = new FintoIvao();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VipiAuth:Enabled"] = "true",
                ["VipiAuth:ClientId"] = FintoIvao.ClientId,
                ["VipiAuth:Authority"] = FintoIvao.Autorita,
                ["VipiAuth:RelaxProtocolValidation"] = "false",
            });
            Assert.True(builder.AddVipiStandaloneAuth());

            // Configure, non PostConfigure: la configurazione statica deve esserci PRIMA che il post-configure
            // del framework decida di andarla a scaricare dall'Authority.
            builder.Services.Configure<OpenIdConnectOptions>(VipiStandaloneAuthExtensions.IvaoScheme, o =>
            {
                o.Configuration = ivao.Configurazione;
                o.Backchannel = new HttpClient(ivao);
            });

            var app = builder.Build();
            app.UseAuthentication();
            app.MapVipiStandaloneAuth();
            await app.StartAsync();
            return new Giro(app, ivao);
        }

        public async Task<Andata> AndataAsync(string returnUrl)
        {
            using var risposta = await _browser.GetAsync($"/services/vsop/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            Assert.Equal(HttpStatusCode.Redirect, risposta.StatusCode);

            return Verso(risposta.Headers.Location!, Cookie(risposta));
        }

        /// <summary>Il ritorno che rimanda a IVAO: il secondo giro partito da solo, coi cookie che ha lasciato.</summary>
        public Andata SecondaAndata(Ritorno ritorno) =>
            Verso(new Uri(ritorno.Destinazione), ritorno.Cookie);

        private Andata Verso(Uri verso, IReadOnlyList<string> cookie)
        {
            Assert.StartsWith(FintoIvao.Autorizza, verso.GetLeftPart(UriPartial.Path));
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(verso.Query);

            var nonce = query["nonce"].ToString();
            Ivao.NonceDaMettere = nonce;     // IVAO, di norma, rimanda il nonce che gli si è dato
            return new Andata(query["state"].ToString(), nonce, cookie);
        }

        public async Task<Ritorno> RitornoAsync(Andata andata, IEnumerable<string> cookie)
        {
            using var richiesta = new HttpRequestMessage(HttpMethod.Get,
                $"/signin-oidc?code=codice-di-prova&state={Uri.EscapeDataString(andata.State)}");
            var elenco = cookie.ToList();
            if (elenco.Count > 0) richiesta.Headers.Add("Cookie", string.Join("; ", elenco));

            using var risposta = await _browser.SendAsync(richiesta);
            Assert.Equal(HttpStatusCode.Redirect, risposta.StatusCode);
            var tornati = Cookie(risposta);
            var sessione = tornati.FirstOrDefault(c => c.StartsWith("vipi.auth=", StringComparison.Ordinal));
            return new Ritorno(risposta.Headers.Location!.OriginalString, sessione, tornati);
        }

        /// <summary>Le proprietà che il cookie di sessione si porta dietro, decifrate come fa l'handler.</summary>
        public Task<IDictionary<string, string?>> ProprietaDellaSessioneAsync(string cookieDiSessione)
        {
            var opzioni = _app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
                .Get(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
            var biglietto = opzioni.TicketDataFormat.Unprotect(cookieDiSessione["vipi.auth=".Length..]);
            Assert.NotNull(biglietto);
            return Task.FromResult<IDictionary<string, string?>>(biglietto!.Properties.Items);
        }

        /// <summary>Solo «nome=valore» di ogni Set-Cookie non cancellato: quello che il browser rimanderebbe.</summary>
        private static List<string> Cookie(HttpResponseMessage risposta) =>
            risposta.Headers.TryGetValues("Set-Cookie", out var valori)
                ? valori.Where(v => !v.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
                    .Select(v => v.Split(';')[0]).ToList()
                : new List<string>();

        public async ValueTask DisposeAsync()
        {
            _browser.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// IVAO per quel che serve al giro: token endpoint e userinfo. L'id_token non è firmato — nel flusso con code
    /// l'handler non chiede la firma (arriva dal canale diretto col server) — e porta il nonce che il test decide.
    /// Utente senza posizioni staff: il caso generico del 28 settembre.
    /// </summary>
    private sealed class FintoIvao : HttpMessageHandler
    {
        public const string Autorita = "https://ivao.prova";
        public const string ClientId = "vipi-prova";
        public const string Autorizza = Autorita + "/authorize";
        private const string Token = Autorita + "/token";
        private const string UserInfo = Autorita + "/userinfo";

        public string NonceDaMettere { get; set; } = "";

        public OpenIdConnectConfiguration Configurazione { get; } = new()
        {
            Issuer = Autorita,
            AuthorizationEndpoint = Autorizza,
            TokenEndpoint = Token,
            UserInfoEndpoint = UserInfo,
            EndSessionEndpoint = Autorita + "/logout",
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var indirizzo = request.RequestUri!.GetLeftPart(UriPartial.Path);
            if (indirizzo == Token)
                return Json(new { access_token = "accesso-di-prova", token_type = "Bearer", expires_in = 3600, id_token = IdToken() });
            if (indirizzo == UserInfo)
                return Json(new { sub = "704798", id = 704798, firstName = "Prova", lastName = "Generica", centerId = "LIRR" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private string IdToken()
        {
            var adesso = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var testa = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "none", typ = "JWT" }));
            var corpo = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
            {
                iss = Autorita, aud = ClientId, sub = "704798", iat = adesso, nbf = adesso, exp = adesso + 300,
                nonce = NonceDaMettere,
            }));
            return $"{testa}.{corpo}.";
        }

        private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static Task<HttpResponseMessage> Json(object corpo) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json"),
            });
    }
}
