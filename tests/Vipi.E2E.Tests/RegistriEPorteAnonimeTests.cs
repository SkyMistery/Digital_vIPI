using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Host;
using Vipi.Hosting;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Revisione totale 3, fetta C di L11: <b>i registri di diagnostica</b> che un anonimo poteva sporcare, svuotare o
/// zittire, e <b>le porte anonime</b> che non avevano un tetto dove serviva. Ogni prova è rossa sul codice di prima.
/// </summary>
public sealed class RegistriEPorteAnonimeTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public RegistriEPorteAnonimeTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    // ---- Una riga è una riga (U-120, U-127, U-023) ---------------------------------------------------------------

    [Fact]
    public void Un_valore_da_fuori_diventa_una_riga_con_un_tetto()
    {
        Assert.Equal("a b c d", TestoDiRegistro.Riga("a\nb\u2028c\td", 50));
        Assert.Equal("abc…", TestoDiRegistro.Riga("abcdef", 3));
        Assert.Null(TestoDiRegistro.Riga(null, 3));
    }

    /// <summary>Il registro degli avvisi con una scrittura finta, come in <c>RegistroAvvisiTests</c>.</summary>
    private sealed class Banco
    {
        public DateTime Ora = new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);
        public string File = "";
        public RegistroAvvisi Registro { get; }
        public ILoggerFactory Fabbrica { get; }

        public Banco(string? esistente = null)
        {
            File = esistente ?? "";
            Registro = new RegistroAvvisi(() => Ora, (testo, intestazione) =>
            {
                if (File.Length == 0 && intestazione is not null) File = intestazione;
                File += testo;
            }, () => File);
            Fabbrica = LoggerFactory.Create(b =>
            {
                b.SetMinimumLevel(LogLevel.Information);
                b.AddProvider(Registro);
                foreach (var (categoria, livello) in RegistroAvvisi.Filtri)
                    b.AddFilter<RegistroAvvisi>(categoria, livello);
            });
        }

        public void Richiesta(string percorso, int esito = 404)
        {
            var stato = new List<KeyValuePair<string, object?>>
            {
                new("ElapsedMilliseconds", 3.0), new("StatusCode", esito), new("Method", "GET"),
                new("PathBase", ""), new("Path", percorso), new("QueryString", ""),
                new("{OriginalFormat}", "Request finished {Method} {Path}{QueryString} - {StatusCode}"),
            };
            Fabbrica.CreateLogger(RegistroAvvisi.CategoriaRichieste).Log(LogLevel.Information, new EventId(2, "RequestFinished"),
                (IReadOnlyList<KeyValuePair<string, object?>>)stato, null, (_, _) => $"Request finished GET {percorso} - {esito}");
        }

        /// <summary>Le righe a colonna 0 che hanno la forma di un'intestazione di voce o di una ripetizione.</summary>
        public int IntestazioniCon(string testo) =>
            Regex.Matches(File, @"^(?:ANCORA )?\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} UTC · .*$", RegexOptions.Multiline)
                .Count(m => m.Value.Contains(testo, StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 U-120: un percorso con %0A (ASP.NET Core lo decodifica) andava a capo dentro una voce e scriveva a colonna 0
    /// un'intestazione finta. Con un timbro di oggi nel futuro, la firma risultava «già scritta» fino a mezzanotte:
    /// l'avviso vero non si scriveva più.
    /// </summary>
    [Fact]
    public void Un_percorso_con_un_a_capo_non_scrive_righe_finte_e_non_zittisce_una_firma()
    {
        var a = new Banco();
        a.Fabbrica.CreateLogger("Vipi.Api").LogWarning("API {Endpoint}: chiave sconosciuta", "archivio");
        var firma = Regex.Match(a.File, "firma ([0-9a-f]{12})").Groups[1].Value;

        var b = new Banco();
        b.Richiesta("/\n2026-09-28 23:59:59 UTC · AVVISO · firma " + firma);
        b.Fabbrica.CreateLogger("Vipi.Import").LogWarning("un altro avviso qualsiasi");
        Assert.Equal(0, b.IntestazioniCon("23:59:59"));

        // E la firma vera, arrivata dopo, si scrive: il registro non l'ha presa per già vista.
        b.Ora = b.Ora.AddMinutes(1);
        b.Fabbrica.CreateLogger("Vipi.Api").LogWarning("API {Endpoint}: chiave sconosciuta", "archivio");
        Assert.Contains("firma " + firma, b.File);
    }

    /// <summary>🔴 U-127: dieci percorsi da 8 kB facevano di una voce 80 kB, e pochi avvisi al giorno ruotavano il file.</summary>
    [Fact]
    public void Le_righe_di_contesto_non_portano_percorsi_da_otto_kilobyte()
    {
        var b = new Banco();
        for (var i = 0; i < 10; i++) b.Richiesta("/" + new string('a', 8000));
        b.Fabbrica.CreateLogger("Vipi.Api").LogWarning("un avviso");

        Assert.True(b.File.Length < 6000, $"voce di {b.File.Length} caratteri");
    }

    /// <summary>🔴 U-120: anche un MESSAGGIO con un a capo (il testo di un'eccezione, un valore) non apre righe a colonna 0.</summary>
    [Fact]
    public void Un_messaggio_con_un_a_capo_resta_dentro_la_sua_voce()
    {
        var b = new Banco();
        b.Fabbrica.CreateLogger("Vipi.Api").LogWarning("valore {X}", "x\n2026-09-28 23:59:59 UTC · AVVISO · firma 0123456789ab");

        Assert.Equal(0, b.IntestazioniCon("23:59:59"));
    }

    /// <summary>🔴 U-124: DataProtection nomina la chiave del key-ring; il file la portava fuori per email.</summary>
    [Fact]
    public void Il_GUID_della_chiave_di_DataProtection_non_entra_nel_registro()
    {
        var b = new Banco();
        b.Fabbrica.CreateLogger("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager")
            .LogWarning("No XML encryptor configured. Key {KeyId:B} may be persisted to storage in unencrypted form.",
                Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));

        Assert.Contains("No XML encryptor configured", b.File);
        Assert.DoesNotContain("0f8fad5b", b.File, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 🔴 U-023: «error» ed «error_description» di /signin-oidc li scrive chiunque, e arrivano decodificati. Con un
    /// a capo aprivano in errori-richieste.txt una voce inventata — codice, VID, stack — che l'analisi per era contava.
    /// </summary>
    [Fact]
    public void L_errore_dichiarato_dal_portale_non_apre_voci_finte()
    {
        var falso = "a\n" + new string('-', 78) + "\n2026-09-27 03:00:00 UTC · codice FALSO";
        var descritto = Vipi.Host.Auth.VipiStandaloneAuthExtensions.Describe("x", falso);
        var voce = DiagnosticaErrori.VoceDiLogin("portale", falso, false, false, "/services\n2026-09-27 03:00:00 UTC · codice FALSO2", null, null);

        Assert.DoesNotContain('\n', descritto);
        Assert.DoesNotContain(voce.Split('\n'), r => r.StartsWith("2026-09-27 03:00:00 UTC", StringComparison.Ordinal));
        Assert.True(Vipi.Host.Auth.VipiStandaloneAuthExtensions.Describe("x", new string('d', 5000)).Length < 400);
    }

    // ---- /Error non svuota il registro (U-022/U-098) ------------------------------------------------------------

    /// <summary>
    /// 🔴 U-022/U-098: /Error è pubblico, e ogni richiesta senza eccezione scriveva una NOTA col Referer lungo quanto
    /// voleva il chiamante. Circa 130 richieste facevano ruotare il file due volte: i guasti veri sparivano.
    /// </summary>
    [Fact]
    public async Task Una_raffica_di_Error_non_cancella_i_guasti_veri()
    {
        var registro = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFile)!;
        var precedente = StartupDiagnostics.Percorso(DiagnosticaErrori.NomeFilePrecedente)!;
        if (File.Exists(registro)) File.Delete(registro);
        if (File.Exists(precedente)) File.Delete(precedente);
        DiagnosticaErrori.AzzeraPaginaSenzaEccezione();

        DiagnosticaErrori.Registra("vero", "GET", "/services/x", null, new InvalidOperationException("GUASTO-VERO"));

        var ora = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);
        // Trecento richieste in cinque minuti: cinque righe, ognuna col conto di quelle taciute.
        for (var i = 0; i < 300; i++)
            DiagnosticaErrori.RegistraPaginaSenzaEccezione("c" + i, "https://x/" + new string('A', 8000), ora.AddSeconds(i));

        var testo = await File.ReadAllTextAsync(registro) + (File.Exists(precedente) ? await File.ReadAllTextAsync(precedente) : "");
        Assert.Contains("GUASTO-VERO", testo);
        Assert.True(testo.Length < 20_000, $"registro di {testo.Length} caratteri");
        Assert.Contains("non scritte", testo);   // chi legge sa che ce ne sono state altre
        DiagnosticaErrori.AzzeraPaginaSenzaEccezione();
    }

    // ---- Password e segreti nei file che si spediscono (U-125, U-124) -----------------------------------------

    /// <summary>🔴 U-125: con Password='ab;cd' la coda «cd'» usciva in chiaro in avvio-diagnostica.txt.</summary>
    [Fact]
    public void Una_password_fra_virgolette_con_un_punto_e_virgola_non_esce()
    {
        var riga = StartupDiagnostics.SenzaPassword("Server=db;Password='zq;wx';Database=vipi");

        Assert.DoesNotContain("wx", riga);
        Assert.DoesNotContain("zq", riga);
        Assert.Contains("db", riga);
        Assert.Contains("vipi", riga);
    }

    /// <summary>🔴 U-124: un file dei segreti malformato faceva uscire il suo nome — la sola protezione che ha.</summary>
    [Fact]
    public void Un_file_dei_segreti_malformato_non_dice_il_suo_nome()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "vipi-segreti-" + Guid.NewGuid().ToString("N"));
        var cartella = Path.Combine(baseDir, "segreti");
        Directory.CreateDirectory(cartella);
        const string Nome = "nome-segretissimo-7f3a.json";
        File.WriteAllText(Path.Combine(cartella, Nome), """{ "a": 1 "b": 2 }""");
        try
        {
            var ex = Assert.ThrowsAny<Exception>(() => SegretiFuoriDalWeb.Carica(new ConfigurationManager(), baseDir));
            Assert.DoesNotContain("segretissimo", ex.ToString());
            Assert.Contains("n. 1", ex.Message);
        }
        finally { Directory.Delete(baseDir, recursive: true); }
    }

    // ---- /Error per ogni metodo (U-234) -----------------------------------------------------------------------

    /// <summary>🔴 U-234: /Error esisteva solo in GET, e il gestore rifà la pipeline col metodo originale.</summary>
    [Fact]
    public async Task La_pagina_d_errore_risponde_anche_a_POST_e_PUT()
    {
        using var client = _factory.CreateClient();
        foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Put })
        {
            DiagnosticaErrori.AzzeraPaginaSenzaEccezione();
            var res = await client.SendAsync(new HttpRequestMessage(metodo, "/Error"));
            Assert.NotEqual(HttpStatusCode.MethodNotAllowed, res.StatusCode);
        }
        DiagnosticaErrori.AzzeraPaginaSenzaEccezione();
    }

    // ---- La cache delle letture anonime (U-102) ---------------------------------------------------------------

    /// <summary>🔴 U-102: un «?x=» a caso faceva una copia nuova a ogni richiesta e scacciava quelle buone.</summary>
    [Fact]
    public void Una_chiave_di_query_sconosciuta_non_si_tiene_in_cache()
    {
        static HttpContext Richiesta(string query)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = "GET";
            ctx.Request.Path = "/services/vsop/libb/airports";
            ctx.Request.QueryString = new QueryString(query);
            return ctx;
        }

        Assert.True(CacheDelleLettureAnonime.Riutilizzabile(Richiesta("?icao=LIBD")));
        Assert.True(CacheDelleLettureAnonime.Riutilizzabile(Richiesta("?icao=LIBD&vista=atc")));
        Assert.False(CacheDelleLettureAnonime.Riutilizzabile(Richiesta("?icao=LIBD&x=8f2a")));
        Assert.False(CacheDelleLettureAnonime.Riutilizzabile(Richiesta("?test=METAR")));
    }

    // ---- Immagini: il 304 senza leggere i byte (U-242) --------------------------------------------------------

    /// <summary>
    /// 🔴 U-242: il 304 lo decideva Results.File DOPO aver letto dal database tutti i byte. Uno sha che NON c'è in
    /// archivio lo prova da solo: se si leggesse, sarebbe 404.
    /// </summary>
    [Fact]
    public async Task Il_304_di_un_immagine_non_legge_il_database()
    {
        var sha = new string('b', 64);
        var req = new HttpRequestMessage(HttpMethod.Get, "/vsop/media/" + sha);
        req.Headers.TryAddWithoutValidation("If-None-Match", $"W/\"{sha}\"");

        var res = await _factory.CreateClient().SendAsync(req);

        Assert.Equal(HttpStatusCode.NotModified, res.StatusCode);
        Assert.True(VipiModuleExtensions.EtichettaGiaInMano($"\"x\", \"{sha}\"", sha));
        Assert.False(VipiModuleExtensions.EtichettaGiaInMano("\"altro\"", sha));
    }

    // ---- Lo stream live: un tetto per persona (U-101) ---------------------------------------------------------

    /// <summary>🔴 U-101: col solo tetto globale (300) una persona sola apriva tutte le connessioni della divisione.</summary>
    [Fact]
    public async Task La_stessa_persona_non_apre_piu_di_cinque_stream()
    {
        // ⚠️ Un VID suo: il conto è per processo, e l'identità di sviluppo la usa anche l'altro test dello stream.
        using var fabbrica = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddScoped<ICurrentUserProvider, UnoSolo>()));
        using var client = fabbrica.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var aperti = new List<HttpResponseMessage>();
        try
        {
            for (var i = 0; i < VipiModuleExtensions.MaxSsePerPersona; i++)
            {
                var r = await client.GetAsync("/vsop/live/atc", HttpCompletionOption.ResponseHeadersRead, cts.Token);
                aperti.Add(r);
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            }

            using var troppo = await client.GetAsync("/vsop/live/atc", HttpCompletionOption.ResponseHeadersRead, cts.Token);
            Assert.Equal(HttpStatusCode.TooManyRequests, troppo.StatusCode);
        }
        finally { foreach (var r in aperti) r.Dispose(); }
    }

    // ---- Porta delle API: le chiavi false non arrivano al database (U-243) ------------------------------------

    /// <summary>
    /// 🔴 U-243: una chiave ben formata ma falsa costava una query anche oltre il tetto per IP. Ora, esaurito il conto
    /// delle chiavi sbagliate di quell'indirizzo, si risponde 429 senza verificare.
    /// </summary>
    [Fact]
    public async Task Oltre_il_tetto_una_chiave_falsa_non_costa_una_verifica()
    {
        var verifiche = new VerificaCheConta();
        using var fabbrica = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<IVerificaChiaveApi>(verifiche);
            s.AddSingleton(new RequestRateLimiter());
        }));
        using var client = fabbrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "vipi_" + new string('z', 40));

        var esiti = new List<HttpStatusCode>();
        for (var i = 0; i < 40; i++)
            esiti.Add((await client.GetAsync("/vsop/api/v1/atc/sessions")).StatusCode);

        Assert.Contains(HttpStatusCode.TooManyRequests, esiti);
        Assert.True(verifiche.Chiamate <= 30, $"{verifiche.Chiamate} verifiche per 40 richieste");
    }

    private sealed class UnoSolo : ICurrentUserProvider
    {
        public CurrentUser? Get() => new(990_101, "Chi apre troppe schede", null, Array.Empty<string>());
    }

    private sealed class VerificaCheConta : IVerificaChiaveApi
    {
        private int _chiamate;
        public int Chiamate => _chiamate;
        public Task<VerificaChiave> VerificaAsync(string chiave, string endpoint, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _chiamate);
            return Task.FromResult(new VerificaChiave(EsitoChiaveApi.Sconosciuta, "vipi_zzzz", null));
        }
    }
}
