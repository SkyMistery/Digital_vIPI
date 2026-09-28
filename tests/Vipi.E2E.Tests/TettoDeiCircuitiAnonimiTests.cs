using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// 🔴 U-237 (revisione totale 3): i circuiti Blazor anonimi non avevano tetto. Uno script che apre migliaia di
/// WebSocket su una pagina pubblica faceva crescere la memoria dell'unico processo fino allo spegnimento, e con lui
/// il sito per tutti. Il committente ha scelto una soglia nel codice (27 settembre 2026).
///
/// <para>Il trasporto si simula con una GET su <c>/_blazor</c> che resta «aperta» finché il test non la lascia
/// andare: è quel che fa un WebSocket, che per il middleware è una richiesta lunga quanto la connessione.</para>
/// </summary>
public sealed class TettoDeiCircuitiAnonimiTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public TettoDeiCircuitiAnonimiTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    private static HttpContext Richiesta(string metodo, string percorso, bool entrato = false)
    {
        var c = new DefaultHttpContext();
        c.Request.Method = metodo;
        c.Request.Path = percorso;
        if (entrato) c.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("vid", "704798") }, "prova"));
        return c;
    }

    /// <summary>Una connessione che resta aperta finché non si chiama <c>Chiudi</c>.</summary>
    private sealed class Connessione
    {
        private readonly TaskCompletionSource _fine = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HttpContext Contesto { get; }
        public Task Corsa { get; }
        public bool Entrata { get; private set; }

        public Connessione(TettoDeiCircuitiAnonimi tetto, bool entrato = false)
        {
            Contesto = Richiesta("GET", "/_blazor", entrato);
            Corsa = tetto.PassaAsync(Contesto, async _ => { Entrata = true; await _fine.Task; });
        }

        public Task Chiudi() { _fine.TrySetResult(); return Corsa; }
    }

    [Fact]
    public async Task Oltre_il_tetto_un_anonimo_non_si_collega()
    {
        var tetto = new TettoDeiCircuitiAnonimi(2);
        var a = new Connessione(tetto);
        var b = new Connessione(tetto);
        var c = new Connessione(tetto);

        Assert.True(a.Entrata && b.Entrata);
        Assert.False(c.Entrata);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, c.Contesto.Response.StatusCode);
        Assert.False(string.IsNullOrEmpty(c.Contesto.Response.Headers.RetryAfter));
        Assert.Equal(2, tetto.Aperti);

        await a.Chiudi();
        await b.Chiudi();
    }

    [Fact]
    public async Task Una_connessione_che_si_chiude_libera_il_posto()
    {
        var tetto = new TettoDeiCircuitiAnonimi(1);
        var a = new Connessione(tetto);
        await a.Chiudi();

        var b = new Connessione(tetto);
        Assert.True(b.Entrata);
        Assert.Equal(1, tetto.Aperti);
        await b.Chiudi();
        Assert.Equal(0, tetto.Aperti);
    }

    [Fact]
    public async Task Chi_e_entrato_non_si_conta_e_non_si_ferma()
    {
        var tetto = new TettoDeiCircuitiAnonimi(1);
        var anonimo = new Connessione(tetto);
        var staff = new Connessione(tetto, entrato: true);

        Assert.True(staff.Entrata);
        Assert.Equal(1, tetto.Aperti);

        await anonimo.Chiudi();
        await staff.Chiudi();
    }

    /// <summary>A sala piena la stretta di mano si rifiuta subito; i messaggi di una connessione già ammessa (le
    /// POST del long polling) passano sempre, e le altre pagine non si toccano.</summary>
    [Fact]
    public async Task A_sala_piena_si_rifiuta_la_stretta_di_mano_e_basta()
    {
        var tetto = new TettoDeiCircuitiAnonimi(1);
        var a = new Connessione(tetto);

        async Task<(bool Passata, int Stato)> Prova(string metodo, string percorso)
        {
            var passata = false;
            var c = Richiesta(metodo, percorso);
            await tetto.PassaAsync(c, _ => { passata = true; return Task.CompletedTask; });
            return (passata, c.Response.StatusCode);
        }

        Assert.Equal((false, 503), await Prova("POST", "/_blazor/negotiate"));
        Assert.Equal((true, 200), await Prova("POST", "/_blazor"));
        Assert.Equal((true, 200), await Prova("GET", "/services/vsop/libb"));
        Assert.Equal((true, 200), await Prova("GET", "/_blazor/initializers"));

        await a.Chiudi();
        Assert.Equal((true, 200), await Prova("POST", "/_blazor/negotiate"));
    }

    [Fact]
    public async Task Tetto_a_zero_vuol_dire_nessun_tetto()
    {
        var tetto = new TettoDeiCircuitiAnonimi(0);
        var a = new Connessione(tetto);
        var b = new Connessione(tetto);
        Assert.True(a.Entrata && b.Entrata);
        await a.Chiudi();
        await b.Chiudi();
    }

    /// <summary>
    /// Il sito vero lo <b>monta</b>, non solo lo registra: si riempie la sala del tetto del processo e si bussa
    /// davvero a <c>/_blazor/negotiate</c>. Svuotata la sala, la stessa richiesta passa.
    /// </summary>
    [Fact]
    public async Task Il_sito_a_sala_piena_rifiuta_la_stretta_di_mano()
    {
        var tetto = _factory.Services.GetRequiredService<TettoDeiCircuitiAnonimi>();
        using var client = _factory.CreateClient();

        var occupate = new List<Connessione>();
        try
        {
            while (occupate.Count <= TettoDeiCircuitiAnonimi.TettoPredefinito)
            {
                var c = new Connessione(tetto);
                if (!c.Entrata) break;
                occupate.Add(c);
            }
            Assert.Equal(TettoDeiCircuitiAnonimi.TettoPredefinito, occupate.Count);

            var piena = await client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null);
            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, piena.StatusCode);
        }
        finally
        {
            foreach (var c in occupate) await c.Chiudi();
        }

        var libera = await client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null);
        Assert.NotEqual(System.Net.HttpStatusCode.ServiceUnavailable, libera.StatusCode);
    }
}
