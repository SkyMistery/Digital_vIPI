using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// «Chiedi alla sorgente se c'è ancora», sul filo. È l'unico adapter che guarda lo <b>status</b> della
/// risposta invece di ridurre ogni errore a <c>null</c>, e questi test fissano proprio quella differenza:
/// un 404 non è un 401, e un elenco vuoto non è una prova di niente.
///
/// <para>Carta: <c>docs/feature/2026-08-26-chiedere-alla-sorgente.md</c>.</para>
/// </summary>
public class SourcePresenceProbeTests : IDisposable
{
    // ⚠️ Questi test leggono i MOTIVI, e dal 1 settembre 2026 i motivi hanno due lingue
    // (Messaggio.Lingua): finiscono nella finestra di eliminazione, che in inglese era mezza tradotta.
    // La cultura si fissa qui una volta per la classe — quella di questa macchina è inglese.
    private readonly Vipi.Application.Tests.CulturaDiProva _lingua = Vipi.Application.Tests.CulturaDiProva.Italiana();

    public void Dispose() => _lingua.Dispose();

    // ── Settore di ACC: dettaglio + elenco dei subcenter dell'ente ───────────────────────────────────

    [Fact]
    public async Task Se_il_dettaglio_risponde_il_settore_c_e_ancora()
    {
        var probe = Probe(new()
        {
            ["/v2/subcenters/LIRR_W_CTR"] = Ok("""{ "composePosition": "LIRR_W_CTR" }"""),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AccSector, "LIRR_W_CTR", "LIRR"));

        Assert.Equal(SourcePresence.Presente, r.Esito);
        Assert.False(r.ProvaLAssenza);
    }

    [Fact]
    public async Task Il_settore_e_assente_solo_se_l_ente_ne_nomina_altri()
    {
        var probe = Probe(new()
        {
            ["/v2/subcenters/LIRR_W_CTR"] = NotFound(),
            ["/v2/centers/LIRR/subcenters"] = Ok("""
                [ { "composePosition": "LIRR_N_CTR" },
                  { "composePosition": "LIRR_S_CTR" },
                  { "composePosition": "LIRR_E_CTR" } ]
                """),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AccSector, "LIRR_W_CTR", "LIRR"));

        Assert.Equal(SourcePresence.Assente, r.Esito);
        Assert.True(r.ProvaLAssenza);
        Assert.Contains("ne elenca 3", r.Motivo);
        Assert.Contains("404", r.Tracce);   // l'audit deve poter raccontare su cosa poggia la cancellazione
    }

    [Fact]
    public async Task Un_elenco_vuoto_non_prova_niente()
    {
        // ⚠️ È la ragione per cui esiste la regola dei due giri: «una risposta a zero elementi non è un
        // errore». Crederle qui rifarebbe più in fretta esattamente l'errore che quella regola evita.
        var probe = Probe(new()
        {
            ["/v2/subcenters/LIRR_W_CTR"] = NotFound(),
            ["/v2/centers/LIRR/subcenters"] = Ok("[]"),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AccSector, "LIRR_W_CTR", "LIRR"));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
        Assert.Contains("due giri", r.Motivo);
    }

    [Fact]
    public async Task Se_la_controprova_non_risponde_non_si_conclude_niente()
    {
        var probe = Probe(new()
        {
            ["/v2/subcenters/LIRR_W_CTR"] = NotFound(),
            ["/v2/centers/LIRR/subcenters"] = Stato(HttpStatusCode.InternalServerError),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AccSector, "LIRR_W_CTR", "LIRR"));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
    }

    [Fact]
    public async Task Se_le_due_risposte_sono_in_disaccordo_vince_la_prudenza()
    {
        // Il dettaglio dice 404, ma l'elenco dell'ente lo nomina: due risposte che si contraddicono non
        // sono una prova d'assenza, e davanti a un dubbio non si cancella.
        var probe = Probe(new()
        {
            ["/v2/subcenters/LIRR_W_CTR"] = NotFound(),
            ["/v2/centers/LIRR/subcenters"] = Ok("""
                [ { "composePosition": "LIRR_N_CTR" }, { "composePosition": "LIRR_W_CTR" } ]
                """),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AccSector, "LIRR_W_CTR", "LIRR"));

        Assert.Equal(SourcePresence.Presente, r.Esito);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Un_guasto_non_e_mai_una_assenza(HttpStatusCode status)
    {
        // ⚠️ Il difetto che questo adapter esiste per non avere. Le porte anagrafiche riducono OGNI risposta
        // non-2xx a null: se la finestra lo leggesse come «sparito», un'ora storta di IVAO — o un token
        // scaduto — diventerebbe il permesso di svuotare il catalogo.
        var probe = Probe(new()
        {
            ["/v2/subcenters/LIRR_W_CTR"] = Stato(status),
            // L'elenco risponderebbe benissimo: non deve bastare, perché la domanda puntuale non ha detto «no».
            ["/v2/centers/LIRR/subcenters"] = Ok("""[ { "composePosition": "LIRR_N_CTR" } ]"""),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AccSector, "LIRR_W_CTR", "LIRR"));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
        Assert.Contains(((int)status).ToString(), r.Motivo);
    }

    [Fact]
    public async Task Senza_l_ente_non_c_e_controprova_possibile()
    {
        var probe = Probe(new() { ["/v2/subcenters/LIRR_W_CTR"] = NotFound() });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AccSector, "LIRR_W_CTR"));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
    }

    // ── Postazione d'aeroporto ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task La_postazione_di_scalo_si_chiede_all_aeroporto()
    {
        var probe = Probe(new()
        {
            ["/v2/ATCPositions/LIRF_GND"] = NotFound(),
            ["/v2/airports/LIRF/ATCPositions"] = Ok("""
                [ { "composePosition": "LIRF_TWR" }, { "composePosition": "LIRF_APP" } ]
                """),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AirportSector, "LIRF_GND", "LIRF"));

        Assert.Equal(SourcePresence.Assente, r.Esito);
        Assert.Contains("LIRF ne elenca 2", r.Motivo);
    }

    [Fact]
    public async Task La_postazione_che_la_sorgente_manda_ancora_e_presente()
    {
        var probe = Probe(new()
        {
            ["/v2/ATCPositions/LIRF_GND"] = Ok("""{ "composePosition": "LIRF_GND", "frequency": 121.9 }"""),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AirportSector, "LIRF_GND", "LIRF"));

        Assert.Equal(SourcePresence.Presente, r.Esito);
    }

    // ── Aeroporto: la controprova è di VITALITÀ, non di appartenenza ─────────────────────────────────

    [Fact]
    public async Task L_aeroporto_e_assente_se_l_anagrafica_del_paese_risponde()
    {
        var probe = Probe(new()
        {
            ["/v2/airports/LIXX"] = NotFound(),
            ["/v2/airports?page=1&countryId=IT"] = Ok("""
                { "items": [ { "icao": "LIRF" }, { "icao": "LIMC" } ], "pages": 3 }
                """),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Airport, "LIXX"));

        Assert.Equal(SourcePresence.Assente, r.Esito);
        Assert.Contains("2 aeroporti nella prima pagina", r.Motivo);
    }

    [Fact]
    public async Task L_aeroporto_non_si_dichiara_assente_se_l_anagrafica_tace()
    {
        var probe = Probe(new()
        {
            ["/v2/airports/LIXX"] = NotFound(),
            ["/v2/airports?page=1&countryId=IT"] = Ok("""{ "items": [] }"""),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Airport, "LIXX"));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
    }

    // ── Ente ACC: l'elenco del paese è insieme domanda e controprova ─────────────────────────────────

    [Fact]
    public async Task L_ente_assente_dall_elenco_del_paese_e_assente()
    {
        var probe = Probe(new(), acc: new AccFinta(new[] { Center("LIMM"), Center("LIBB") }));

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Acc, "LIRR"));

        Assert.Equal(SourcePresence.Assente, r.Esito);
        Assert.Contains("2 center", r.Motivo);
    }

    [Fact]
    public async Task L_ente_elencato_dal_paese_e_presente()
    {
        var probe = Probe(new(), acc: new AccFinta(new[] { Center("LIRR"), Center("LIMM") }));

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Acc, "LIRR"));

        Assert.Equal(SourcePresence.Presente, r.Esito);
    }

    [Fact]
    public async Task Se_l_anagrafica_degli_enti_lancia_non_si_sa()
    {
        // ⚠️ `GetCentersByCountryAsync` lancia sia sull'errore HTTP sia sull'elenco vuoto: è già la
        // distinzione che serve qui, e va tradotta in «non si sa», mai in «assente».
        var probe = Probe(new(), acc: new AccFinta(null));

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Acc, "LIRR"));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
    }

    // ── I casi in cui non si chiede affatto ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Senza_credenziali_non_si_chiede_niente()
    {
        var probe = Probe(new(), configurato: false);

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Airport, "LIRF"));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
        Assert.Contains("credenziali", r.Motivo);
    }

    [Fact]
    public async Task Una_rete_che_cade_e_un_verdetto_non_un_eccezione()
    {
        // La porta promette di non lanciare: chi chiama sta già mostrando una finestra, e un'eccezione lì
        // sarebbe un messaggio d'errore al posto di un verdetto.
        var probe = Probe(new(), esplode: true);

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Airport, "LIRF"));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
    }

    [Fact]
    public async Task La_chiave_vuota_non_arriva_alla_rete()
    {
        var probe = Probe(new(), esplode: true);

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.Airport, "   "));

        Assert.Equal(SourcePresence.NonSiSa, r.Esito);
    }

    // ── Impalcatura ──────────────────────────────────────────────────────────────────────────────────

    private static SourceCenter Center(string code) => new($"{code}_CTR", code, code, false);

    private static (HttpStatusCode Status, string Body) Ok(string body) => (HttpStatusCode.OK, body);
    private static (HttpStatusCode Status, string Body) NotFound() => (HttpStatusCode.NotFound, "");
    private static (HttpStatusCode Status, string Body) Stato(HttpStatusCode s) => (s, "");

    /// <summary>
    /// Lo stesso verdetto per chi legge in inglese: è il giro che il difetto del 1 settembre 2026 lasciava
    /// scoperto — la finestra tradotta e dentro, in italiano, la risposta della sorgente.
    /// </summary>
    [Fact]
    public async Task Il_verdetto_esce_nella_lingua_di_chi_legge()
    {
        using var _ = Vipi.Application.Tests.CulturaDiProva.Inglese();

        var probe = Probe(new()
        {
            ["/v2/subcenters/LIRR_W_CTR"] = NotFound(),
            ["/v2/centers/LIRR/subcenters"] = Ok(SUBCENTERS),
        });

        var r = await probe.ChiediAsync(new SourceProbeTarget(SourceProbeKind.AccSector, "LIRR_W_CTR", "LIRR"));

        Assert.Equal(SourcePresence.Assente, r.Esito);
        Assert.Contains("LIRR lists 2 of them", r.Motivo);
    }

    private const string SUBCENTERS = "[{ \"composePosition\": \"LIRR_E_CTR\" }, { \"composePosition\": \"LIRR_N_CTR\" }]";

    private static IvaoSourcePresenceProbe Probe(
        Dictionary<string, (HttpStatusCode Status, string Body)> risposte,
        IAccDirectory? acc = null, bool configurato = true, bool esplode = false)
    {
        var opt = Options.Create(new IvaoOptions { ClientId = configurato ? "prova" : "", ClientSecret = "x" });
        var http = new HttpClient(new Centralino(risposte, esplode));
        var token = new IvaoTokenProvider(new FabbricaConToken(), opt);
        return new IvaoSourcePresenceProbe(new IvaoHttp(http, token, opt), opt, acc ?? new AccFinta(null));
    }

    /// <summary>Risponde secondo il percorso chiesto; un percorso non previsto è un 404, come in rete.</summary>
    private sealed class Centralino : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _r;
        private readonly bool _esplode;

        public Centralino(Dictionary<string, (HttpStatusCode, string)> r, bool esplode)
        {
            _r = r;
            _esplode = esplode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            if (_esplode) throw new HttpRequestException("la rete non c'è");

            var percorso = req.RequestUri!.PathAndQuery;
            var (status, body) = _r.TryGetValue(percorso, out var hit)
                ? hit
                : (HttpStatusCode.NotFound, "");

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>Il token: una risposta finta, per non uscire in rete dal provider.</summary>
    private sealed class FabbricaConToken : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Stampella());

        private sealed class Stampella : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"finto","expires_in":3600}""",
                        Encoding.UTF8, "application/json"),
                });
        }
    }

    /// <summary>L'anagrafica enti: <c>null</c> = lancia, come fa quella vera su errore o su elenco vuoto.</summary>
    private sealed class AccFinta : IAccDirectory
    {
        private readonly IReadOnlyList<SourceCenter>? _centers;
        public AccFinta(IReadOnlyList<SourceCenter>? centers) => _centers = centers;

        public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) =>
            GetCentersByCountryAsync("IT", ct);

        public Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default) =>
            _centers is null
                ? throw new InvalidOperationException("/v2/centers: nessun ACC riconosciuto")
                : Task.FromResult(_centers);

        public Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(
            string accIcao, IReadOnlySet<string> skipDetailIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
