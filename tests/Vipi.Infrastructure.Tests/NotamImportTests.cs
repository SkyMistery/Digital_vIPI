using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// I NOTAM della divisione dalla sorgente all'archivio (carta
/// <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>).
///
/// <para>Il corpo di prova è <b>vero</b>: due delle quattro voci che <c>/v2/divisions/IT/notams/all</c> dava il
/// 12 ottobre 2026, con la mappa accorciata a un punto. Più una terza scritta a mano per le forme che l'Italia quel
/// giorno non aveva ma il resto del mondo sì: date vuote e <c>specialAreaId</c> nullo.</para>
/// </summary>
public sealed class NotamImportTests : IAsyncLifetime
{
    private const string Percorso = "/v2/divisions/IT/notams/all";

    private const string Corpo = """
        [
          {
            "regionMap": [ { "lat": 43.97, "lng": 10.422222222222 } ],
            "regionMapPolygon": [ [ 10.422222222222, 43.97 ] ],
            "createdAt": "2025-11-05T22:46:23.000Z",
            "updatedAt": "2026-06-18T19:36:37.000Z",
            "id": 4845,
            "name": "IT-B002/25",
            "specialAreaId": null,
            "startTime": "2025-11-27T00:00:00.000Z",
            "endTime": "2027-01-01T00:00:00.000Z",
            "military": false,
            "description": "Q) LIXX/QSPXX/IV/BO /E /000/185/4344N01107E034\r\nA) LIMM LIRR B) 2511270000 C) PERM\r\nE) FIRENZE CTR FIRENZE CTA. APPROACH CONTROL CHG AS FOLLOWS:\r\n- LIMM-LIRQ VLOA\r\nREF https://it.ivao.aero/atc/briefing-loa",
            "centers": [ { "military": false, "id": "LIRR", "countryId": "IT", "name": "Roma" } ],
            "airports": [ { "military": false, "icao": "LIRQ", "iata": "FLR", "name": "Peretola", "city": "Firenze", "countryId": "IT" } ]
          },
          {
            "regionMap": [ { "lat": 40.49916667, "lng": 15.52888889 } ],
            "regionMapPolygon": [ [ 15.52888889, 40.49916667 ] ],
            "createdAt": "2026-08-31T07:46:06.000Z",
            "updatedAt": "2026-08-31T09:10:53.000Z",
            "id": 5163,
            "name": "ITW004/26",
            "specialAreaId": 10477,
            "startTime": "2026-08-31T08:41:00.000Z",
            "endTime": "2026-08-31T23:00:00.000Z",
            "military": true,
            "description": "TSA 'DONALD WEST' ACTIVE DURING ONLINE DAY (1900-2130Z EST) DUE TO MIL ACT.",
            "centers": [
              { "military": true, "id": "LIBB", "countryId": "IT", "name": "Brindisi" },
              { "military": true, "id": "LIZZ", "countryId": "IT", "name": "Legion" }
            ],
            "airports": [
              { "military": true, "icao": "LIBA", "iata": null, "name": "Amendola", "city": "Foggia", "countryId": "IT" },
              { "military": true, "icao": "LIBV", "iata": null, "name": "Gioia del Colle", "city": "Bari", "countryId": "IT" }
            ]
          },
          {
            "id": 16,
            "name": "A4710/22",
            "specialAreaId": 0,
            "startTime": "",
            "endTime": "",
            "military": false,
            "description": "PERMANENTE, SENZA DATE",
            "centers": [],
            "airports": [ { "icao": "lirf" } ]
          }
        ]
        """;

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private static readonly DateTime Adesso = new(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // ---- Dal corpo ai NOTAM -----------------------------------------------------------------------------

    [Fact]
    public void Il_corpo_vero_si_legge()
    {
        var notam = IvaoNotamClient.Leggi(Corpo, Percorso);

        Assert.Equal(new[] { "IT-B002/25", "ITW004/26", "A4710/22" }, notam.Select(n => n.Nome));

        var firenze = notam[0];
        Assert.Equal(4845, firenze.IvaoId);
        Assert.Equal(new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc), firenze.InizioUtc);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), firenze.FineUtc);
        Assert.Equal(DateTimeKind.Utc, firenze.InizioUtc!.Value.Kind);
        Assert.Equal(new[] { "LIRQ" }, firenze.Aeroporti);
        Assert.Equal(new[] { "LIRR" }, firenze.Centri);
        Assert.False(firenze.Militare);
        Assert.Equal(new DateTime(2026, 6, 18, 19, 36, 37, DateTimeKind.Utc), firenze.AggiornatoUtc);

        var tsa = notam[1];
        Assert.True(tsa.Militare);
        Assert.Equal(new[] { "LIBA", "LIBV" }, tsa.Aeroporti);
        Assert.Equal(new[] { "LIBB", "LIZZ" }, tsa.Centri);
    }

    /// <summary>La sorgente manda le righe con CRLF: in archivio una forma sola, o il riquadro (che divide per
    /// <c>\n</c>) si porterebbe dietro un ritorno a capo in coda a ogni riga.</summary>
    [Fact]
    public void Le_righe_del_testo_arrivano_con_un_solo_a_capo()
    {
        var testo = IvaoNotamClient.Leggi(Corpo, Percorso)[0].Testo;

        Assert.DoesNotContain('\r', testo);
        Assert.Equal(5, testo.Split('\n').Length);
        Assert.StartsWith("Q) LIXX/QSPXX", testo);
        Assert.EndsWith("https://it.ivao.aero/atc/briefing-loa", testo);
    }

    /// <summary>Date vuote = non dette: senza inizio e senza fine. I codici escono maiuscoli.</summary>
    [Fact]
    public void Le_date_vuote_sono_non_dette()
    {
        var permanente = IvaoNotamClient.Leggi(Corpo, Percorso)[2];

        Assert.Null(permanente.InizioUtc);
        Assert.Null(permanente.FineUtc);
        Assert.Null(permanente.AggiornatoUtc);
        Assert.Equal(new[] { "LIRF" }, permanente.Aeroporti);
        Assert.Empty(permanente.Centri);
    }

    /// <summary>
    /// ⚠️ Una fine scritta che non si legge NON diventa «senza fine»: fallisce il giro. Altrimenti un NOTAM
    /// scaduto resterebbe in testa ai documenti per sempre, e nessuno saprebbe perché.
    /// </summary>
    [Theory]
    [InlineData("""[{"id":1,"name":"X","endTime":"domani"}]""")]
    [InlineData("""[{"id":1,"name":"X","startTime":"31/12"}]""")]
    [InlineData("""[{"name":"SENZA ID"}]""")]
    [InlineData("""[{"id":7}]""")]
    [InlineData("""[{"id":"sette","name":"ID NON NUMERICO"}]""")]
    [InlineData("""{"message":"non un elenco"}""")]
    public void Un_corpo_che_non_torna_fa_fallire_il_giro(string corpo) =>
        Assert.Throws<InvalidDataException>(() => IvaoNotamClient.Leggi(corpo, Percorso));

    [Fact]
    public void Un_elenco_vuoto_e_un_elenco_vuoto() => Assert.Empty(IvaoNotamClient.Leggi("[]", Percorso));

    // ---- La chiamata -----------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 Il percorso della divisione, <b>senza</b> <c>now=true</c> (con quello la sorgente perde il filtro della
    /// divisione: 166 NOTAM di tutto il mondo al posto di 4) e <b>senza</b> token: l'endpoint è pubblico, e
    /// chiedere un token vorrebbe dire che un segreto ruotato male spegne anche i NOTAM (U-025).
    /// </summary>
    [Fact]
    public async Task Chiede_la_divisione_senza_filtro_e_senza_token()
    {
        var centralino = new Centralino(HttpStatusCode.OK, Corpo);

        var notam = await Client(centralino).GetDivisionNotamsAsync();

        Assert.Equal(3, notam.Count);
        var richiesta = Assert.Single(centralino.Richieste);
        Assert.Equal(Percorso, richiesta.Uri.AbsolutePath);
        Assert.Equal("", richiesta.Uri.Query);
        Assert.False(richiesta.ConToken);
    }

    /// <summary>Ogni non-2xx solleva, 404 compreso: qui un 404 è «percorso sbagliato», non «nessun NOTAM».</summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Un_non_2xx_non_e_un_elenco_vuoto(HttpStatusCode status)
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(new Centralino(status, "")).GetDivisionNotamsAsync());

        Assert.Equal(status, ex.StatusCode);
    }

    // ---- L'archivio ------------------------------------------------------------------------------------

    private static NotamDaSorgente Voce(int id, string nome, string testo = "E) PROVA", DateTime? fine = null,
                                        params string[] aeroporti) =>
        new(id, nome, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), fine, testo, false,
            aeroporti.Length == 0 ? new[] { "LIRF" } : aeroporti, new[] { "LIRR" },
            new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc));

    private EfNotamStore Archivio() => new(_db);

    [Fact]
    public async Task Il_primo_giro_crea_le_righe()
    {
        var esito = await Archivio().SincronizzaAsync(IvaoNotamClient.Leggi(Corpo, Percorso), Adesso);

        Assert.Equal(new NotamImportResult(3, 0, 0), esito);
        _db.ChangeTracker.Clear();
        var firenze = await _db.Notams.SingleAsync(n => n.IvaoId == 4845);
        Assert.Equal("IT-B002/25", firenze.Name);
        Assert.Equal("LIRQ", firenze.Airports);
        Assert.Equal("LIRR", firenze.Centers);
        Assert.Equal(new DateTime(2027, 1, 1), firenze.EndUtc);
        Assert.Equal(Adesso, firenze.ImportedAtUtc);
        Assert.Null((await _db.Notams.SingleAsync(n => n.IvaoId == 16)).EndUtc);
    }

    /// <summary>A regime il giro rilegge le stesse voci ogni mezz'ora: non deve riscrivere niente, né spostare il
    /// timbro — «importato adesso» di un NOTAM fermo da mesi sarebbe falso.</summary>
    [Fact]
    public async Task Un_giro_senza_novita_non_tocca_niente()
    {
        await Archivio().SincronizzaAsync(IvaoNotamClient.Leggi(Corpo, Percorso), Adesso);
        _db.ChangeTracker.Clear();

        var esito = await Archivio().SincronizzaAsync(IvaoNotamClient.Leggi(Corpo, Percorso), Adesso.AddMinutes(30));

        Assert.Equal(new NotamImportResult(0, 0, 0), esito);
        _db.ChangeTracker.Clear();
        Assert.All(await _db.Notams.ToListAsync(), n => Assert.Equal(Adesso, n.ImportedAtUtc));
    }

    [Fact]
    public async Task Un_NOTAM_corretto_alla_sorgente_si_aggiorna()
    {
        await Archivio().SincronizzaAsync(new[] { Voce(1, "IT-A001/26"), Voce(2, "IT-A002/26") }, Adesso);
        _db.ChangeTracker.Clear();

        var fine = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var esito = await Archivio().SincronizzaAsync(
            new[] { Voce(1, "IT-A001/26", "E) CORRETTO", fine, "LIRF", "LIRA"), Voce(2, "IT-A002/26") }, Adesso.AddHours(1));

        Assert.Equal(new NotamImportResult(0, 1, 0), esito);
        _db.ChangeTracker.Clear();
        var riga = await _db.Notams.SingleAsync(n => n.IvaoId == 1);
        Assert.Equal("E) CORRETTO", riga.Text);
        Assert.Equal(fine, riga.EndUtc);
        Assert.Equal("LIRA LIRF", riga.Airports);
        Assert.Equal(Adesso.AddHours(1), riga.ImportedAtUtc);
    }

    /// <summary>Un NOTAM che la sorgente non elenca più è stato cancellato dallo staff: sparisce anche da noi,
    /// in vigore o no.</summary>
    [Fact]
    public async Task Un_NOTAM_tolto_alla_sorgente_sparisce()
    {
        await Archivio().SincronizzaAsync(new[] { Voce(1, "IT-A001/26"), Voce(2, "IT-A002/26") }, Adesso);
        _db.ChangeTracker.Clear();

        var esito = await Archivio().SincronizzaAsync(new[] { Voce(2, "IT-A002/26") }, Adesso.AddHours(1));

        Assert.Equal(new NotamImportResult(0, 0, 1), esito);
        Assert.Equal(new[] { 2 }, (await Archivio().ListAsync()).Select(n => n.IvaoId));
    }

    /// <summary>La stessa voce due volte nell'elenco non urta l'indice univoco: vale l'ultima.</summary>
    [Fact]
    public async Task Una_voce_ripetuta_non_rompe_il_giro()
    {
        var esito = await Archivio().SincronizzaAsync(
            new[] { Voce(1, "IT-A001/26", "E) PRIMA"), Voce(1, "IT-A001/26", "E) DOPO") }, Adesso);

        Assert.Equal(new NotamImportResult(1, 0, 0), esito);
        Assert.Equal("E) DOPO", Assert.Single(await Archivio().ListAsync()).Text);
    }

    // ---- Il giro ---------------------------------------------------------------------------------------

    private sealed class Sorgente(Func<IReadOnlyList<NotamDaSorgente>> elenco) : INotamSource
    {
        public Task<IReadOnlyList<NotamDaSorgente>> GetDivisionNotamsAsync(CancellationToken ct = default) =>
            Task.FromResult(elenco());
    }

    private sealed class Orologio(DateTime adesso) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(adesso);
    }

    /// <summary>🔴 Sorgente che non risponde: l'archivio resta com'era. I documenti continuano a mostrare i NOTAM
    /// dell'ultimo giro riuscito — e quelli scaduti spariscono lo stesso, perché lo decide la data.</summary>
    [Fact]
    public async Task Se_la_sorgente_non_si_legge_l_archivio_resta_com_era()
    {
        await Archivio().SincronizzaAsync(new[] { Voce(1, "IT-A001/26"), Voce(2, "IT-A002/26") }, Adesso);
        _db.ChangeTracker.Clear();
        var giro = new NotamImportUseCase(
            new Sorgente(() => throw new HttpRequestException("IVAO 503")), Archivio(), new Orologio(Adesso));

        await Assert.ThrowsAsync<HttpRequestException>(() => giro.RunAsync());

        Assert.Equal(2, (await Archivio().ListAsync()).Count);
    }

    [Fact]
    public async Task Il_giro_porta_l_elenco_in_archivio_e_i_documenti_lo_leggono()
    {
        var giro = new NotamImportUseCase(
            new Sorgente(() => IvaoNotamClient.Leggi(Corpo, Percorso)), Archivio(), new Orologio(Adesso));

        Assert.Equal(new NotamImportResult(3, 0, 0), await giro.RunAsync());
        _db.ChangeTracker.Clear();

        var lettore = new NotamDeiDocumenti(Archivio(), NullLogger<NotamDeiDocumenti>.Instance, new Orologio(Adesso));
        // Firenze vede il suo, in vigore.
        var firenze = Assert.Single(await lettore.PerAsync(AmbitoNotam.Aeroporti(new[] { "LIRQ" })));
        Assert.Equal("IT-B002/25", firenze.Nome);
        Assert.False(firenze.InArrivo);
        // Amendola no: il suo è scaduto il 31 agosto, anche se la sorgente lo elenca ancora.
        Assert.Empty(await lettore.PerAsync(AmbitoNotam.Aeroporti(new[] { "LIBA" })));
        // Fiumicino sì: quello senza date non scade.
        Assert.Equal("A4710/22", Assert.Single(await lettore.PerAsync(AmbitoNotam.Aeroporti(new[] { "LIRF" }))).Nome);
    }

    private sealed class ArchivioRotto : INotamStore
    {
        public Task<IReadOnlyList<Notam>> ListAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("archivio giù");
        public Task<NotamImportResult> SincronizzaAsync(IReadOnlyList<NotamDaSorgente> arrivati, DateTime adessoUtc,
                                                        CancellationToken ct = default) =>
            throw new InvalidOperationException("archivio giù");
    }

    /// <summary>⚠️ Chi legge per la pagina non lancia mai: il riquadro è un di più, e non deve portarsi dietro il
    /// documento che il lettore ha chiesto.</summary>
    [Fact]
    public async Task Un_archivio_che_non_risponde_da_una_pagina_senza_riquadro()
    {
        var lettore = new NotamDeiDocumenti(new ArchivioRotto(), NullLogger<NotamDeiDocumenti>.Instance);

        Assert.Empty(await lettore.PerAsync(AmbitoNotam.Aeroporti(new[] { "LIRQ" })));
    }

    // ---- La cadenza ------------------------------------------------------------------------------------

    /// <summary>La pagina Sorgenti annuncia la cadenza che il giro usa davvero: stesso numero, stesso minimo.</summary>
    [Theory]
    [InlineData(30, 30)]
    [InlineData(10, 10)]
    [InlineData(0, 5)]
    [InlineData(-3, 5)]
    public void La_cadenza_annunciata_e_quella_del_giro(int minuti, int attesi)
    {
        var opt = new IvaoOptions { NotamImportMinutes = minuti };
        var cadenze = new ImportSchedule(Options.Create(opt), Options.Create(new SectorfileOptions()));

        Assert.Equal(TimeSpan.FromMinutes(attesi), opt.NotamImportPeriod);
        Assert.Equal(opt.NotamImportPeriod, cadenze.PeriodOf(ImportCategories.Notam));
    }

    // ---- Finti -----------------------------------------------------------------------------------------

    private static IvaoNotamClient Client(Centralino centralino)
    {
        // Con un ClientId: se il client chiedesse il token per sbaglio, il test lo vedrebbe nell'intestazione.
        var opt = Options.Create(new IvaoOptions { ClientId = "prova", ClientSecret = "x" });
        var http = new IvaoHttp(new HttpClient(centralino), new IvaoTokenProvider(new FabbricaConToken(), opt), opt);
        return new IvaoNotamClient(http, opt, Options.Create(new DivisionOptions()));
    }

    private sealed class Centralino(HttpStatusCode status, string corpo) : HttpMessageHandler
    {
        public List<(Uri Uri, bool ConToken)> Richieste { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Richieste.Add((req.RequestUri!, req.Headers.Authorization is not null));
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FabbricaConToken : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Stampella());

        private sealed class Stampella : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"finto","expires_in":3600}""", Encoding.UTF8, "application/json"),
                });
        }
    }
}
