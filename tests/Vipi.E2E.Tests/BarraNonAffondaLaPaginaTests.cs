using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// La barra in cima è il CONTORNO: se non riesce a decidere una sua decorazione, la pagina si serve lo
/// stesso senza quella decorazione.
///
/// <para><b>Il difetto vero, del 24 agosto 2026.</b> Un socio senza incarichi — cioè chiunque entri per la
/// prima volta — ha visto «An error occurred while processing your request.» su <c>/services</c>, che è
/// l'elenco degli strumenti e non legge una riga di database; lo stesso indirizzo, senza accedere,
/// rispondeva 200. L'unica cosa che un utente loggato fa in più di un anonimo su quella pagina era la
/// domanda «hai qualcosa da modificare?», e quella domanda va al database: l'anonimo non ci arriva
/// (<c>_user is not null</c> è falso) e l'admin nemmeno (esce sui codici staff). Restava a pagarla il
/// socio qualunque, e un intoppo qualsiasi del database diventava una pagina d'errore <b>per i soli
/// utenti loggati</b> — mentre ogni sonda anonima continuava a dire che il sito era su.</para>
/// </summary>
public sealed class BarraNonAffondaLaPaginaTests
{
    /// <summary>Le quattro pagine del contorno pubblico, per un socio qualunque: devono aprirsi.</summary>
    [Theory]
    [InlineData("/services")]
    [InlineData("/services/vsop")]
    [InlineData("/services/vsop/live")]
    [InlineData("/services/vsop/guide")]
    public async Task Un_socio_senza_incarichi_apre_le_pagine(string percorso)
    {
        using var fabbrica = new FabbricaSocio();

        var res = await fabbrica.CreateClient().GetAsync(percorso);

        await Assert200(percorso, res);
    }

    // ⚠️ Qui c'era «Se_la_domanda_della_barra_fallisce_la_pagina_esce_lo_stesso», che spegneva le
    // concessioni e chiedeva che la pagina uscisse comunque. Il 28 agosto 2026 quella domanda è stata
    // TOLTA, non resa tollerante: il tasto «Modifica» ora si decide sul livello, che sta nei claim e in
    // memoria. Un test che finge di rompere una query che nessuno fa più proverebbe soltanto sé stesso.
    // Il difetto che raccontava resta nella carta e in `docs/lavori-aperti.md` §U.

    /// <summary>
    /// La versione è una spia per chi amministra: a un socio non dice niente, e a chiunque passi di qui
    /// direbbe con quale build precisa sta parlando — informazione da regalare solo a chi serve.
    /// </summary>
    [Fact]
    public async Task Al_socio_la_versione_non_si_mostra()
    {
        using var fabbrica = new FabbricaSocio();

        var html = await fabbrica.CreateClient().GetStringAsync("/services");

        Assert.DoesNotContain("ver-chip", html);
    }

    /// <summary>
    /// ⚠️ Il difetto vero del 24 agosto 2026, letto nello stack trace di 78 richieste fallite in produzione:
    /// il catalogo delle ACC veniva chiesto <b>dentro</b> <c>BuildRenderTree</c>. Blazor disegna l'albero
    /// mentre <c>OnParametersSetAsync</c> è ancora in volo, quindi quella query partiva sullo stesso
    /// <c>DbContext</c> su cui era già in corso quella di <c>_canEdit</c> — «A second operation was started
    /// on this context instance».
    ///
    /// <para>Il test non riproduce la corsa (dipenderebbe da un tempo), ma l'<b>invariante</b> che la
    /// esclude: quando il render chiede il catalogo, qualcuno l'ha già scaldato fuori dal render. Se
    /// qualcuno rimette <c>Stations.Accs</c> nel markup, qui si vede.</para>
    /// </summary>
    [Fact]
    public async Task Il_catalogo_si_scalda_fuori_dal_render()
    {
        var catalogo = new CatalogoCheRicordaLOrdine();
        using var fabbrica = new FabbricaSocio(catalogo);

        var res = await fabbrica.CreateClient().GetAsync("/services");

        await Assert200("/services", res);
        Assert.True(catalogo.Prewarmed, "nessuno ha chiamato Prewarm(): il catalogo lo sta caricando il "
                                      + "render, ed è la corsa sul DbContext che ha buttato giù /services.");
        Assert.False(catalogo.LettoPrimaDelPrewarm,
            "il catalogo è stato letto PRIMA di essere scaldato: se la lettura avviene dentro il render, "
            + "cade sullo stesso DbContext della domanda che il layout sta ancora aspettando.");
    }

    /// <summary>Catalogo illeggibile: la barra perde i collegamenti alle ACC, la pagina no.</summary>
    [Fact]
    public async Task Se_il_catalogo_non_si_legge_la_pagina_esce_senza_i_collegamenti()
    {
        using var fabbrica = new FabbricaSocio(new CatalogoRotto());

        var res = await fabbrica.CreateClient().GetAsync("/services");

        await Assert200("/services", res);
        var html = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("acc-nav\"><a", html);   // nessun collegamento ACC, ma la pagina c'è
    }

    /// <summary>Registra l'ordine: chi chiede il catalogo, e se qualcuno l'ha scaldato prima.</summary>
    private sealed class CatalogoCheRicordaLOrdine : IStationResolver
    {
        public bool Prewarmed { get; private set; }
        public bool LettoPrimaDelPrewarm { get; private set; }

        public IReadOnlyList<AccInfo> Accs
        {
            get
            {
                if (!Prewarmed) LettoPrimaDelPrewarm = true;
                return Array.Empty<AccInfo>();
            }
        }

        public void Prewarm() => Prewarmed = true;
        public AccInfo? Resolve(string accCode) => null;
        public AccInfo? ResolveByCallsign(string callsign) => null;
        // Questo finto catalogo esiste per registrare l'ORDINE delle chiamate, non per risolvere aeroporti.
        public AirportStation? Airport(string? icao) => null;
        public AirportStation? AirportOfCallsign(string? callsign) => null;
    }

    /// <summary>Il catalogo non si legge: è il database che non risponde.</summary>
    private sealed class CatalogoRotto : IStationResolver
    {
        private static Exception Giu() => new InvalidOperationException("catalogo non leggibile (simulato)");
        public IReadOnlyList<AccInfo> Accs => throw Giu();
        public void Prewarm() => throw Giu();
        public AccInfo? Resolve(string accCode) => throw Giu();
        public AccInfo? ResolveByCallsign(string callsign) => throw Giu();
        // Anche queste cadono: il punto del test è che la pagina regga un catalogo che NON risponde, e
        // un'eccezione in meno qui sarebbe una prova più debole.
        public AirportStation? Airport(string? icao) => throw Giu();
        public AirportStation? AirportOfCallsign(string? callsign) => throw Giu();
    }

    private static async Task Assert200(string percorso, HttpResponseMessage res)
    {
        var corpo = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK,
            $"{percorso} -> {(int)res.StatusCode}\n{corpo[..Math.Min(1500, corpo.Length)]}");
    }

    /// <summary>Identità di un socio qualunque: nessuna posizione staff ⇒ non admin, e nessuna concessione.</summary>
    private sealed class SocioSemplice : ICurrentUserProvider
    {
        public CurrentUser? Get() => new(123456, "Mario Rossi", "LIRR", Array.Empty<string>());
    }

    private sealed class FabbricaSocio : WebApplicationFactory<Program>
    {
        private readonly IStationResolver? _catalogo;
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-socio-{Guid.NewGuid():N}.db");

        public FabbricaSocio(IStationResolver? catalogo = null) => _catalogo = catalogo;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<ICurrentUserProvider>();
                s.AddScoped<ICurrentUserProvider, SocioSemplice>();
                if (_catalogo is not null)
                {
                    s.RemoveAll<IStationResolver>();
                    s.AddSingleton(_catalogo);
                }
            });
            // Come le altre fabbriche E2E: niente OIDC reale in CI (vedi SmokeTests.VipiAppFactory).
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort */ }
        }
    }
}
