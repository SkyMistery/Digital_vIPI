using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vipi.Host;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il ponte fra le copie di «RFO Gate Manager» (carta <c>docs/feature/2026-09-18-ponte-rfo-gate-manager.md</c>), sul
/// server vero: ogni riga del contratto del programma (<c>docs/SYNC-API.md</c> della sua repo) e il
/// comportamento di <c>FakeSyncServer</c>, che ne è la forma eseguibile.
///
/// <para>🔴 La prova che conta di più è <see cref="Molti_PUT_simultanei_con_la_stessa_versione_ne_scrive_uno_solo"/>:
/// controllo e scrittura atomici, mai due 200 sulla stessa versione.</para>
/// </summary>
public sealed class PonteRfoTests : IClassFixture<PonteRfoTests.PonteFactory>
{
    internal const string Chiave = "rfo_chiave-di-prova-lunga-almeno-trentadue-caratteri";
    internal const string ChiaveGlobale = "rfo_chiave-globale-di-prova-lunga-almeno-trentadue";
    internal const string ChiaveAltroEvento = "rfo_chiave-di-un-altro-evento-lunga-trentadue-car";
    internal const string ChiaveCorta = "corta";

    private readonly PonteFactory _factory;
    public PonteRfoTests(PonteFactory factory) => _factory = factory;

    private static string Url(string evento) => $"/api/rfo/events/{evento}/state";

    /// <summary>Un evento nuovo per ogni prova: la fixture è condivisa, i documenti no.</summary>
    private static string Evento() => "prova-" + Guid.NewGuid().ToString("N")[..12];

    private HttpClient Client(string? chiave = ChiaveGlobale)
    {
        var c = _factory.CreateClient();
        if (chiave is not null) c.DefaultRequestHeaders.Add("x-api-key", chiave);
        return c;
    }

    private static HttpRequestMessage Put(string evento, string? ifMatch, string corpo)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, Url(evento))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        if (ifMatch is not null) req.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return req;
    }

    private static HttpRequestMessage Get(string evento, string? ifNoneMatch = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Url(evento));
        if (ifNoneMatch is not null) req.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        return req;
    }

    private static async Task<JsonElement> Busta(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    // --- La sezione «Come verificare che funziona» del contratto, nell'ordine ---

    [Fact]
    public async Task I_cinque_passi_del_contratto()
    {
        var c = Client();
        var ev = Evento();

        // 1. Documento mai scritto → 404, corpo vuoto.
        var r1 = await c.SendAsync(Get(ev));
        Assert.Equal(HttpStatusCode.NotFound, r1.StatusCode);
        Assert.Equal("", await r1.Content.ReadAsStringAsync());

        // 2. Creazione → 200, version 1, ETag "1".
        var r2 = await c.SendAsync(Put(ev, "\"0\"", """{"updatedBy":"TEST","data":{"pins":{"A":"14"}}}"""));
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal("\"1\"", r2.Headers.ETag?.ToString());
        var b2 = await Busta(r2);
        Assert.Equal(1, b2.GetProperty("version").GetInt64());
        Assert.Equal("TEST", b2.GetProperty("updatedBy").GetString());
        Assert.Equal("14", b2.GetProperty("data").GetProperty("pins").GetProperty("A").GetString());
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$", b2.GetProperty("updatedAt").GetString());

        // 3. Niente di nuovo → 304 senza corpo.
        var r3 = await c.SendAsync(Get(ev, "\"1\""));
        Assert.Equal(HttpStatusCode.NotModified, r3.StatusCode);
        Assert.Equal("", await r3.Content.ReadAsStringAsync());

        // 4. Scrittura su versione vecchia → 409 con la busta alla versione 1.
        var r4 = await c.SendAsync(Put(ev, "\"0\"", """{"updatedBy":"TEST","data":{"pins":{"B":"22"}}}"""));
        Assert.Equal(HttpStatusCode.Conflict, r4.StatusCode);
        Assert.Equal("\"1\"", r4.Headers.ETag?.ToString());
        var b4 = await Busta(r4);
        Assert.Equal(1, b4.GetProperty("version").GetInt64());
        Assert.Equal("14", b4.GetProperty("data").GetProperty("pins").GetProperty("A").GetString());

        // 5. Senza If-Match → 428. Senza chiave → 401, corpo vuoto.
        Assert.Equal(HttpStatusCode.PreconditionRequired,
            (await c.SendAsync(Put(ev, null, """{"data":{}}"""))).StatusCode);
        var r5 = await Client(chiave: null).SendAsync(Get(ev));
        Assert.Equal(HttpStatusCode.Unauthorized, r5.StatusCode);
        Assert.Equal("", await r5.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Molti_PUT_simultanei_con_la_stessa_versione_ne_scrive_uno_solo()
    {
        var ev = Evento();
        Assert.Equal(HttpStatusCode.OK,
            (await Client().SendAsync(Put(ev, "\"0\"", """{"updatedBy":"BASE","data":{}}"""))).StatusCode);

        // Dieci postazioni che partono tutte dalla versione 1, nello stesso istante.
        var via = new TaskCompletionSource();
        var colpi = Enumerable.Range(0, 10).Select(async i =>
        {
            var c = Client();
            await via.Task;
            return await c.SendAsync(Put(ev, "\"1\"", $"{{\"updatedBy\":\"POS{i}\",\"data\":{{\"k\":{i}}}}}"));
        }).ToList();
        via.SetResult();
        var risposte = await Task.WhenAll(colpi);

        Assert.Equal(1, risposte.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, risposte.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var perdente in risposte.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Equal(2, (await Busta(perdente)).GetProperty("version").GetInt64());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();
        Assert.Equal(2, (await db.RfoSharedStates.AsNoTracking().SingleAsync(s => s.EventId == ev)).Version);
        // La storia: una riga per scrittura riuscita, non una di più.
        Assert.Equal(new long[] { 1, 2 },
            await db.RfoSharedStateHistory.AsNoTracking().Where(h => h.EventId == ev).OrderBy(h => h.Version)
                .Select(h => h.Version).ToArrayAsync());
    }

    [Fact]
    public async Task Due_creazioni_simultanee_una_crea_l_altra_riceve_409_con_la_busta()
    {
        var ev = Evento();
        var via = new TaskCompletionSource();
        var colpi = Enumerable.Range(0, 6).Select(async i =>
        {
            var c = Client();
            await via.Task;
            return await c.SendAsync(Put(ev, "\"0\"", $"{{\"updatedBy\":\"POS{i}\",\"data\":{{\"k\":{i}}}}}"));
        }).ToList();
        via.SetResult();
        var risposte = await Task.WhenAll(colpi);

        Assert.Equal(1, risposte.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, risposte.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var perdente in risposte.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Equal(1, (await Busta(perdente)).GetProperty("version").GetInt64());
    }

    /// <summary>Il caso di <c>FakeSyncServer</c>: due postazioni partono dalla stessa versione; la seconda riceve il
    /// 409, riapplica la sua modifica sulla busta e riprova — e le due modifiche si sommano.</summary>
    [Fact]
    public async Task Due_scritture_dalla_stessa_versione_si_sommano_col_409()
    {
        var c = Client();
        var ev = Evento();
        await c.SendAsync(Put(ev, "\"0\"", """{"updatedBy":"GND","data":{"pins":{"AZA100":"14"}}}"""));

        var conflitto = await c.SendAsync(Put(ev, "\"0\"", """{"updatedBy":"TWR","data":{"pins":{"RYR200":"22"}}}"""));
        Assert.Equal(HttpStatusCode.Conflict, conflitto.StatusCode);
        var busta = await Busta(conflitto);
        var versione = busta.GetProperty("version").GetInt64();
        Assert.Equal("14", busta.GetProperty("data").GetProperty("pins").GetProperty("AZA100").GetString());

        var ok = await c.SendAsync(Put(ev, $"\"{versione}\"",
            """{"updatedBy":"TWR","data":{"pins":{"AZA100":"14","RYR200":"22"}}}"""));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(2, (await Busta(ok)).GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Documento_inesistente_con_If_Match_diverso_da_zero_404()
    {
        var r = await Client().SendAsync(Put(Evento(), "\"2\"", """{"updatedBy":"GND","data":{"a":1}}"""));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("", await r.Content.ReadAsStringAsync());
    }

    // --- Il contenuto è opaco ---

    [Fact]
    public async Task Data_torna_indietro_identico_byte_per_byte()
    {
        var c = Client();
        var ev = Evento();
        // Spazi, ordine delle chiavi, caratteri non ASCII, numeri con esponente, annidamenti: niente si riscrive.
        const string data = """{ "z": 1, "a" : [1e3, 2.50, "città ✈"], "n": null, "o": {"é": {"deep": [ {} ]}} }""";

        var put = await c.SendAsync(Put(ev, "\"0\"", "{\"updatedBy\":\"GND\",\"data\":" + data + "}"));
        Assert.Contains("\"data\":" + data, await put.Content.ReadAsStringAsync());

        var get = await c.SendAsync(Get(ev));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Contains("\"data\":" + data, await get.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdatedBy_si_salva_com_e_e_oltre_64_caratteri_si_tronca()
    {
        var c = Client();
        var ev = Evento();
        var lungo = new string('X', 80);
        var r = await c.SendAsync(Put(ev, "\"0\"", "{\"updatedBy\":\"" + lungo + "\",\"data\":{}}"));
        Assert.Equal(new string('X', 64), (await Busta(r)).GetProperty("updatedBy").GetString());

        var senza = await c.SendAsync(Put(ev, "\"1\"", """{"data":{}}"""));
        Assert.Equal(HttpStatusCode.OK, senza.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Busta(senza)).GetProperty("updatedBy").ValueKind);
    }

    [Theory]
    [InlineData("""{"updatedBy":"GND"}""")]                  // data assente
    [InlineData("""{"updatedBy":"GND","data":[1,2]}""")]     // non è un oggetto
    [InlineData("""{"updatedBy":"GND","data":"testo"}""")]
    [InlineData("""{"updatedBy":"GND","data":null}""")]
    [InlineData("""[{"data":{}}]""")]                         // il corpo non è un oggetto
    [InlineData("""{"updatedBy":42,"data":{}}""")]           // updatedBy non è un testo: 422, non un 500
    public async Task Corpo_senza_un_oggetto_data_422(string corpo)
    {
        var r = await Client().SendAsync(Put(Evento(), "\"0\"", corpo));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task Json_rotto_400()
    {
        var r = await Client().SendAsync(Put(Evento(), "\"0\"", """{"data":{"""));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Corpo_oltre_un_mega_413_anche_senza_Content_Length()
    {
        var grosso = "{\"data\":{\"x\":\"" + new string('a', 1_048_576) + "\"}}";

        var dichiarato = await Client().SendAsync(Put(Evento(), "\"0\"", grosso));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, dichiarato.StatusCode);

        // A blocchi: la lunghezza non c'è, il tetto si conta leggendo.
        var req = new HttpRequestMessage(HttpMethod.Put, Url(Evento()))
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(grosso))),
        };
        req.Headers.TryAddWithoutValidation("If-Match", "\"0\"");
        req.Headers.TransferEncodingChunked = true;
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Client().SendAsync(req)).StatusCode);
    }

    // --- Le intestazioni ---

    [Fact]
    public async Task ETag_debole_riscritto_da_un_proxy_vale_come_quello_forte()
    {
        var c = Client();
        var ev = Evento();
        await c.SendAsync(Put(ev, "\"0\"", """{"data":{}}"""));

        Assert.Equal(HttpStatusCode.NotModified, (await c.SendAsync(Get(ev, "W/\"1\""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(Put(ev, "W/\"1\"", """{"data":{"a":1}}"""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(Get(ev, "\"1\""))).StatusCode);   // vecchio → corpo
    }

    [Theory]
    [InlineData("*")]
    [InlineData("\"-1\"")]
    [InlineData("\"uno\"")]
    [InlineData("\"1\", \"2\"")]
    public async Task If_Match_che_non_e_una_versione_400(string ifMatch)
    {
        var r = await Client().SendAsync(Put(Evento(), ifMatch, """{"data":{}}"""));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task La_busta_non_si_tiene_in_nessuna_cache_e_non_c_e_CORS()
    {
        var c = Client();
        var ev = Evento();
        var req = Put(ev, "\"0\"", """{"data":{}}""");
        req.Headers.Add("Origin", "https://esempio.invalid");
        var r = await c.SendAsync(req);

        Assert.True(r.Headers.CacheControl is { Private: true, NoCache: true }, r.Headers.CacheControl?.ToString());
        Assert.False(r.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // --- La porta ---

    [Theory]
    [InlineData("Prova-Maiuscola")]
    [InlineData("con_sottolineato")]
    [InlineData("troppo-lungo-0123456789012345678901234567890123456789012345678901234")]
    public async Task EventId_fuori_dal_modello_400(string evento)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Client().SendAsync(Get(evento))).StatusCode);
    }

    [Fact]
    public async Task Chiave_sbagliata_o_corta_401_senza_corpo()
    {
        foreach (var chiave in new[] { Chiave + "x", ChiaveCorta, "" })
        {
            var r = await Client(chiave).SendAsync(Get("lirn-20260919"));
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            Assert.Equal("", await r.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Chiave_di_un_altro_evento_403_quella_globale_apre_tutto()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(ChiaveAltroEvento).SendAsync(Get("lirn-20260919"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client(ChiaveAltroEvento).SendAsync(Get("limc-20261010"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client(ChiaveGlobale).SendAsync(Get(Evento()))).StatusCode);

        // La chiave dell'evento vero apre il suo evento e nessun altro.
        Assert.Equal(HttpStatusCode.NotFound, (await Client(Chiave).SendAsync(Get("lirn-20260919"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Chiave).SendAsync(Get("limc-20261010"))).StatusCode);
    }

    [Fact]
    public async Task Il_registro_delle_richieste_non_scrive_i_304_del_ponte()
    {
        Assert.True(RegistroRichieste.PollingVuoto("/api/rfo/events/lirn-20260919/state", 304));
        Assert.False(RegistroRichieste.PollingVuoto("/api/rfo/events/lirn-20260919/state", 200));
        Assert.False(RegistroRichieste.PollingVuoto("/api/rfo/events/lirn-20260919/state", 409));
        Assert.False(RegistroRichieste.PollingVuoto("/services/vsop/airport/LIRN", 304));
    }

    public sealed class PonteFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-e2e-rfo-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
                // Le prove usano eventi «prova-…» nuovi a ogni giro, con la chiave globale.
                ["Rfo:Chiavi:lirn:Chiave"] = Chiave,
                ["Rfo:Chiavi:lirn:Eventi"] = "lirn-20260919",
                ["Rfo:Chiavi:globale:Chiave"] = ChiaveGlobale,
                ["Rfo:Chiavi:globale:Eventi"] = "*",
                ["Rfo:Chiavi:limc:Chiave"] = ChiaveAltroEvento,
                ["Rfo:Chiavi:limc:Eventi"] = "limc-20261010",
                ["Rfo:Chiavi:corta:Chiave"] = ChiaveCorta,
                ["Rfo:Chiavi:corta:Eventi"] = "*",
            }));
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort cleanup */ }
        }
    }
}
