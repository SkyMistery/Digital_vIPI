using System.Net;
using Microsoft.Extensions.Options;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La lettura delle piste da <c>/v2/airports/{icao}/runways</c>, e in particolare <b>l'unità della
/// lunghezza</b>.
///
/// <para><b>Perché esiste.</b> Fino al 7 settembre 2026 la sorgente mandava <c>length</c> in <b>piedi</b> e
/// il client la convertiva (<c>* 0.3048</c>); da quella data la manda già in <b>metri</b> e la conversione è
/// sparita. Il campo <b>non ha cambiato nome quando ha cambiato unità</b>, quindi niente, in tutta la catena,
/// direbbe che è tornata la moltiplicazione: <c>SourceRunway.LengthM</c> si chiamerebbe uguale e la pista
/// uscirebbe lunga un terzo. Questo test è l'unico posto in cui quell'unità è verificata invece che
/// commentata.</para>
///
/// <para>⚠️ Ed è il primo test che esercita <see cref="IvaoAirportDetailClient"/>: tutti gli altri
/// (<c>AirportDataImportTests</c> e compagnia) sostituiscono <c>IAirportDetailProvider</c> con un falso e gli
/// passano una lunghezza <b>già convertita</b>, quindi la conversione non è mai stata provata da nessuno.</para>
///
/// <para>Il corpo di risposta è ritagliato da quello vero, letto sul filo il 29 agosto 2026 su LIPI —
/// stesso esempio che sta nel commento di <c>RunwayDto</c>, col numero riletto come metri.</para>
/// </summary>
public class IvaoAirportRunwaysTests
{
    private const string Base = "https://api.test";
    private const string UrlPiste = Base + "/v2/airports/LIPI/runways";

    /// <summary>Due soglie della stessa pista, come le manda la sorgente: una riga PER SOGLIA.</summary>
    private const string DuePiste = """
        [{"id":11609,"airportIcao":"LIPI","runway":"RW06","length":2990,"bearing":57,
          "latitude":45.9735305556,"longitude":13.0350638889,"elevation":162,"width":44},
         {"id":11610,"airportIcao":"LIPI","runway":"RW24","length":2990,"bearing":237,
          "latitude":45.9800000000,"longitude":13.0500000000,"elevation":160,"width":44}]
        """;

    private sealed class Handler : HttpMessageHandler
    {
        private readonly string _corpo;
        public Handler(string corpo) => _corpo = corpo;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(request.RequestUri!.ToString() == UrlPiste
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_corpo) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private sealed class NessunaFabbrica : IHttpClientFactory
    {
        // Senza ClientId il token non si chiede nemmeno (IvaoTokenProvider.GetTokenAsync esce subito):
        // questa fabbrica esiste solo perché il costruttore la pretende, e non deve essere usata.
        public HttpClient CreateClient(string name) => throw new InvalidOperationException(
            "Senza credenziali il token non si chiede: se si arriva qui, è cambiato il giro dell'autorizzazione.");
    }

    private static IvaoAirportDetailClient Costruisci(string corpo)
    {
        var opt = Options.Create(new IvaoOptions { BaseUrl = Base });   // niente ClientId: nessun token
        var http = new IvaoHttp(new HttpClient(new Handler(corpo)), new IvaoTokenProvider(new NessunaFabbrica(), opt), opt);
        return new IvaoAirportDetailClient(http, opt);
    }

    /// <summary>
    /// ⚠️ <b>Il numero esce come è entrato.</b> 2990 metri restano 2990: se qualcuno rimettesse la
    /// conversione dai piedi uscirebbero 911, e la pista di Rivolto diventerebbe più corta di quella di
    /// un aeroclub senza che niente segnali l'errore.
    /// </summary>
    [Fact]
    public async Task La_lunghezza_arriva_in_metri_e_non_si_converte()
    {
        var piste = await Costruisci(DuePiste).GetRunwaysAsync("LIPI");

        Assert.Equal(2, piste.Count);
        Assert.All(piste, p => Assert.Equal(2990, p.LengthM));
    }

    /// <summary>Il resto della riga continua a essere letto: il prefisso <c>RW</c> cade, la soglia e la sua
    /// quota arrivano intere. Sta qui perché è la stessa risposta, e un test che guarda un campo solo non
    /// dice se gli altri sono sopravvissuti alla modifica.</summary>
    [Fact]
    public async Task Identificativo_soglia_e_quota_restano_quelli()
    {
        var piste = await Costruisci(DuePiste).GetRunwaysAsync("LIPI");

        var sei = piste.Single(p => p.Ident == "06");
        Assert.Equal(57, sei.Bearing);
        Assert.Equal(45.9735305556, sei.ThresholdLat!.Value, 9);
        Assert.Equal(13.0350638889, sei.ThresholdLon!.Value, 9);
        Assert.Equal(162, sei.ElevationFt);
    }

    /// <summary>Una lunghezza assente o non positiva resta <c>null</c>, non diventa zero: «non lo so» e
    /// «lunga zero» sono due cose diverse, e la seconda finirebbe stampata in un documento.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("0")]
    public async Task Senza_lunghezza_la_pista_resta_senza(string valore)
    {
        var corpo = $$"""[{"runway":"RW06","length":{{valore}},"bearing":57}]""";

        var pista = Assert.Single(await Costruisci(corpo).GetRunwaysAsync("LIPI"));

        Assert.Null(pista.LengthM);
    }
}
